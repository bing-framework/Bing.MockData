using System;
using System.Collections.Generic;
using Bing.MockData.Catalogs;

namespace Bing.MockData.Education
{
    public sealed partial class Course {public string Code {get;set;}public string Name {get;set;}public string Major {get;set;}public int Credits {get;set;}}
    public sealed partial class Student {public string Id {get;set;}public string Name {get;set;}public CatalogRecord Institution {get;set;}public string Class {get;set;}public string Major {get;set;}}
    public sealed class Enrollment {public Student Student {get;set;}public Course Course {get;set;}public decimal Grade {get;set;}public DateTimeOffset EnrolledAt {get;set;}public DateTimeOffset AssessedAt {get;set;}}
    public sealed partial class EducationData
    {
        private readonly MockContext _c;internal EducationData(MockContext c){_c=c;}
        public CatalogRecord Institution(string country=null)=>_c.Catalog("institutions").Sample(_c.Numbers.ULong(),new CatalogQuery{Kind="institution",CountryCode=country,CancellationToken=_c.CancellationToken});
        public CatalogRecord Book()=>_c.Catalog("books").Sample(_c.Numbers.ULong(),new CatalogQuery{Kind="book",CancellationToken=_c.CancellationToken});
        public DataSetInfo Coverage(string catalog)=>_c.Catalog(catalog).Info;
        public IEnumerable<CatalogRecord> FindInstitutions(string country)=>_c.Catalog("institutions").Query(new CatalogQuery{Kind="institution",CountryCode=country},_c.CancellationToken);
        public string Isbn13(){string body="978"+_c.Text.Random(9,9,"0123456789");return body+CommerceData.BarcodeCheckDigit(body);}
        /// <summary>生成模拟课程，允许显式指定专业、课程名和学分。</summary>
        public MockBuilder<Course> Course(Major major=null,string name=null,int? credits=null)
        {
            var frozen=major==null?null:CopyMajor(major);if(name!=null)Text(name);if(credits.HasValue&&(credits<1||credits>30))throw new ArgumentOutOfRangeException(nameof(credits));
            var builder=_c.Builder<Course>();
            if(frozen==null)builder.Nested(x=>x.MajorDefinition,Major());
            else builder.RuleFor(x=>x.MajorDefinition,c=>{c.ForObject();return CopyMajor(frozen);});
            return builder.RuleFor(x=>x.Code,c=>c.Identifiers.Business("COURSE-"))
                .RuleFor(x=>x.Name,(c,x)=>name??CourseName(c,x.MajorDefinition)).RuleFor(x=>x.Major,(_,x)=>x.MajorDefinition.Name)
                .RuleFor(x=>x.Credits,c=>credits??c.Numbers.Int(1,6));
        }
        /// <summary>生成关联同一机构、班级和专业的模拟学生。</summary>
        public MockBuilder<Student> Student(string country=null)
        {
            var classroom=_c.Builder<AcademicClass>().RuleFor(x=>x.Institution,c=>c.Education().Institution(country))
                .Nested(x=>x.Major,Major()).RuleFor(x=>x.Code,c=>c.Identifiers.Business("CLASS-")).RuleFor(x=>x.EntryYear,c=>c.ReferenceTime.Year-c.Numbers.Int(0,3));
            return _c.Builder<Student>().Nested(x=>x.AcademicClass,classroom).RuleFor(x=>x.Id,c=>c.Identifiers.Business("STU-"))
                .RuleFor(x=>x.Name,c=>c.Text.Name()).RuleFor(x=>x.Class,(_,x)=>x.AcademicClass.Code)
                .RuleFor(x=>x.Institution,(_,x)=>x.AcademicClass.Institution).RuleFor(x=>x.Major,(_,x)=>x.AcademicClass.Major.Name);
        }
        public MockBuilder<Enrollment> Enrollment(IReadOnlyList<Student> students,IReadOnlyList<Course> courses)
        {if(students==null||students.Count==0||courses==null||courses.Count==0)throw new ArgumentException("需要学生和课程集合。");var people=new List<Student>(students).AsReadOnly();var subjects=new List<Course>(courses).AsReadOnly();return _c.Builder<Enrollment>().RuleFor(x=>x.Student,c=>c.Collections.Pick(people)).RuleFor(x=>x.Course,c=>c.Collections.Pick(subjects)).RuleFor(x=>x.Grade,c=>c.Numbers.Decimal(0,100,1)).RuleFor(x=>x.EnrolledAt,c=>c.ReferenceTime.AddDays(-180)).RuleFor(x=>x.AssessedAt,c=>c.ReferenceTime.AddDays(-1));}
    }
    public static class EducationExtensions {public static EducationData Education(this MockContext context)=>new EducationData(context??throw new ArgumentNullException(nameof(context)));}
}
