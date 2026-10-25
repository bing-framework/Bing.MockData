#pragma warning disable xUnit1051
using System;
using System.Linq;
using Bing.MockData.Catalogs;
using Bing.MockData.Education;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Domains.Tests;

public class EducationGapTests
{
    [Fact]
    public void Cohort_has_consistent_references_unique_enrollment_and_replay()
    {
        using var c=new MockContext(new MockOptions{Seed=37}.UseCatalogFile("institutions",IndustryGapTests.Data("ror.sqlite3")));
        var institution=c.Education().Institution();var major=new Major{Code="CS",Name="计算机科学"};
        var g=c.Education().Cohort(institution,major,2,4,3,2).Build();major.Name="modified";
        var result=g.At(0);Assert.Equal("计算机科学",result.Major.Name);Assert.Equal(2,result.Classes.Count);Assert.Equal(8,result.Students.Count);Assert.Equal(16,result.Enrollments.Count);
        foreach(var room in result.Classes){Assert.Same(institution,room.Institution);Assert.Same(result.Major,room.Major);}
        foreach(var student in result.Students){Assert.Contains(student.AcademicClass,result.Classes);Assert.Equal(student.Class,student.AcademicClass.Code);Assert.Equal(result.Major.Name,student.Major);}
        foreach(var enrollment in result.Enrollments)
        {
            Assert.Contains(enrollment.Student,result.Students);Assert.Contains(enrollment.Course,result.Courses);Assert.Same(result.Major,enrollment.Course.MajorDefinition);
            Assert.InRange(enrollment.Grade,0,100);Assert.True(enrollment.EnrolledAt<=enrollment.AssessedAt);Assert.True(enrollment.AssessedAt<=c.ReferenceTime);
        }
        Assert.Equal(result.Enrollments.Count,result.Enrollments.Select(e=>(e.Student.Id,e.Course.Code)).Distinct().Count());
        Assert.Equal(g.Enumerate(8).Select(JsonConvert.SerializeObject),g.EnumerateParallel(8,4).Select(JsonConvert.SerializeObject));
        Assert.Throws<MockLimitException>(()=>c.Education().Cohort(institution,major,2,500));
        using var shallow=new MockContext(new MockOptions{Limits=new MockLimits{MaxDepth=1}});
        Assert.Throws<MockLimitException>(()=>shallow.Education().Cohort(institution,major));
    }

    [Fact]
    public void Standalone_templates_expose_structured_class_and_course_configuration()
    {
        using var c=new MockContext(new MockOptions{Seed=37}.UseCatalogFile("institutions",IndustryGapTests.Data("ror.sqlite3")));
        var major=c.Education().Major().Build().At(0);var room=c.Education().Class(c.Education().Institution(),major,2020).Build().At(0);
        var student=c.Education().Student(room).Build().At(0);Assert.Same(room,student.AcademicClass);Assert.Equal(room.Major.Name,student.Major);
        var course=c.Education().Course(major,"定制课程",8).Build().At(0);Assert.Equal(major.Code,course.MajorDefinition.Code);Assert.Equal("定制课程",course.Name);Assert.Equal(8,course.Credits);
        var automatic=c.Education().Student().Build().At(0);Assert.Equal(automatic.Class,automatic.AcademicClass.Code);Assert.Same(automatic.Institution,automatic.AcademicClass.Institution);
    }

    [Fact]
    public void Structured_books_preserve_source_fields_and_missing_values()
    {
        using var c=new MockContext(new MockOptions{Seed=37}.UseCatalogFile("books",IndustryGapTests.Data("crossref-books.sqlite3")));
        foreach(var record in c.Catalog("books").Query(new CatalogQuery{Kind="book"}).Take(20))
        {
            var book=c.Education().BookDetails(record);var raw=JObject.Parse(record.PayloadJson);
            Assert.Same(record,book.Source);Assert.Equal((string)raw["DOI"],book.Doi);
            Assert.Equal((raw["title"] as JArray??new JArray()).Values<string>(),book.Titles);
            Assert.Equal((raw["ISBN"] as JArray??new JArray()).Values<string>(),book.Isbns);
            Assert.Equal((string)raw["publisher"],book.Publisher?.Name);
            var authors=raw["author"] as JArray??new JArray();Assert.Equal(authors.Count,book.Authors.Count);
            for(int i=0;i<authors.Count;i++){Assert.Equal((string)authors[i]["family"],book.Authors[i].Family);Assert.Equal((string)authors[i]["ORCID"],book.Authors[i].Orcid);}
            var parts=raw["published"]?["date-parts"]?.First as JArray;Assert.Equal(parts?.Count>2?(int?)parts[2]:null,book.PublicationDay);
        }
    }
}
