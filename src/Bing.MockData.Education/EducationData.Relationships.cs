using System;
using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Catalogs;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Education
{
    public sealed partial class EducationData
    {
        private string Text(string value)
        {
            _c.CancellationToken.ThrowIfCancellationRequested();
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(value.Length>_c.Limits.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),_c.Limits.MaxTextLength);
            return value;
        }

        private Major CopyMajor(Major source)
        {
            if(source==null||string.IsNullOrWhiteSpace(source.Code)||string.IsNullOrWhiteSpace(source.Name))throw new ArgumentException("专业代码和名称不能为空。");
            return new Major{Code=Text(source.Code),Name=Text(source.Name)};
        }

        private static string CourseName(MockContext c,Major major)
        {
            string[] names=major.Code=="CS"?(c.Locale=="zh-CN"?new[]{"程序设计","数据结构","软件工程"}:new[]{"Programming","Data structures","Software engineering"})
                :major.Code=="BUS"?(c.Locale=="zh-CN"?new[]{"组织管理","市场分析","商业统计"}:new[]{"Management","Market analysis","Business statistics"})
                :major.Code=="ART"?(c.Locale=="zh-CN"?new[]{"设计基础","色彩理论","艺术史"}:new[]{"Design foundations","Color theory","Art history"})
                :new[]{major.Name+(c.Locale=="zh-CN"?"导论":" introduction")};
            return c.Collections.Pick(names);
        }

        /// <summary>生成模拟专业；不声称真实机构开设该专业。</summary>
        public MockBuilder<Major> Major()=>_c.Builder<Major>().RuleFor(x=>x.Code,c=>c.Collections.Pick(new[]{"CS","BUS","ART"}))
            .RuleFor(x=>x.Name,(c,x)=>x.Code=="CS"?(c.Locale=="zh-CN"?"计算机科学":"Computer science"):x.Code=="BUS"?(c.Locale=="zh-CN"?"工商管理":"Business administration"):(c.Locale=="zh-CN"?"艺术设计":"Art and design"));

        /// <summary>生成指定真实机构和模拟专业的班级，专业配置在调用时复制。</summary>
        public MockBuilder<AcademicClass> Class(CatalogRecord institution,Major major,int? entryYear=null)
        {
            InstitutionRecord(institution);var frozen=CopyMajor(major);int year=entryYear??_c.ReferenceTime.Year;
            if(year<1||year>_c.ReferenceTime.Year)throw new ArgumentOutOfRangeException(nameof(entryYear));
            return _c.Builder<AcademicClass>().RuleFor(x=>x.Institution,_=>institution)
                .RuleFor(x=>x.Major,c=>c.ForObject().Education().CopyMajor(frozen)).RuleFor(x=>x.EntryYear,_=>year)
                .RuleFor(x=>x.Code,c=>c.Identifiers.Business("CLASS-"));
        }

        /// <summary>生成引用已有班级的学生；调用方在生成期间应保持该班级不变。</summary>
        public MockBuilder<Student> Student(AcademicClass classroom)
        {
            if(classroom==null)throw new ArgumentNullException(nameof(classroom));InstitutionRecord(classroom.Institution);CopyMajor(classroom.Major);Text(classroom.Code);
            return _c.Builder<Student>().RuleFor(x=>x.AcademicClass,_=>classroom).RuleFor(x=>x.Id,c=>c.Identifiers.Business("STU-"))
                .RuleFor(x=>x.Name,c=>c.Text.Name()).RuleFor(x=>x.Class,(_,x)=>x.AcademicClass.Code)
                .RuleFor(x=>x.Institution,(_,x)=>x.AcademicClass.Institution).RuleFor(x=>x.Major,(_,x)=>x.AcademicClass.Major.Name);
        }

        private static void InstitutionRecord(CatalogRecord record)
        {if(record==null||record.Kind!="institution")throw new ArgumentException("需要机构目录记录。",nameof(record));}

        /// <summary>生成有界的同专业教学场景，选课不重复且所有引用均属于该场景。</summary>
        public MockBuilder<EducationCohort> Cohort(CatalogRecord institution,Major major,int classes=2,int studentsPerClass=10,int courses=3,int coursesPerStudent=2)
        {
            InstitutionRecord(institution);var frozen=CopyMajor(major);
            if(classes<1||studentsPerClass<0||courses<1||coursesPerStudent<0||coursesPerStudent>courses)throw new ArgumentOutOfRangeException(nameof(classes));
            long studentCount=(long)classes*studentsPerClass;
            // Bound operands before multiplying again to avoid overflow for malicious sizes.
            long limit=_c.Limits.MaxCollectionSize;
            if(studentCount>limit||classes>limit||courses>limit||coursesPerStudent>limit)throw new MockLimitException(nameof(MockLimits.MaxCollectionSize),limit);
            long count=1L+classes+courses+studentCount+studentCount*coursesPerStudent;
            if(count>limit)throw new MockLimitException(nameof(MockLimits.MaxCollectionSize),limit);
            _c.ForObject().ForObject();
            return _c.Builder<EducationCohort>().ConstructUsing(c=>
            {
                var subject=c.ForField("major").ForObject().Education().CopyMajor(frozen);
                var rooms=new List<AcademicClass>();var subjects=new List<Course>();var people=new List<Student>();var enrollments=new List<Enrollment>();
                for(int i=0;i<courses;i++)
                {
                    var f=c.ForField("course["+i+"]").ForObject();
                    subjects.Add(new Course{Code=f.Education().Text(f.Identifiers.Business("COURSE-")+"-"+i),Name=f.Education().Text(CourseName(f,subject)),Major=subject.Name,MajorDefinition=subject,Credits=f.Numbers.Int(1,6)});
                }
                for(int i=0;i<classes;i++)
                {
                    var f=c.ForField("class["+i+"]").ForObject();
                    var room=new AcademicClass{Code=f.Education().Text(f.Identifiers.Business("CLASS-")+"-"+i),Institution=institution,Major=subject,EntryYear=c.ReferenceTime.Year};rooms.Add(room);
                    for(int j=0;j<studentsPerClass;j++)
                    {
                        var s=c.ForField("student["+i+","+j+"]").ForObject();
                        var student=new Student{Id=s.Education().Text(s.Identifiers.Business("STU-")+"-"+i+"-"+j),Name=s.Text.Name(),Institution=institution,AcademicClass=room,Class=room.Code,Major=subject.Name};people.Add(student);
                        foreach(var course in s.Collections.Sample(subjects,coursesPerStudent))
                        {
                            var e=c.ForField("enrollment["+i+","+j+","+course.Code+"]").ForObject();
                            var start=new DateTimeOffset(c.ReferenceTime.Year,1,1,0,0,0,TimeSpan.Zero);
                            if(start>c.ReferenceTime)start=c.ReferenceTime;
                            var middle=start.AddTicks((c.ReferenceTime-start).Ticks/2);
                            enrollments.Add(new Enrollment{Student=student,Course=course,Grade=e.Numbers.Decimal(0,100,1),EnrolledAt=start,AssessedAt=middle});
                        }
                    }
                }
                return new EducationCohort{Institution=institution,Major=subject,Classes=rooms.AsReadOnly(),Courses=subjects.AsReadOnly(),Students=people.AsReadOnly(),Enrollments=enrollments.AsReadOnly()};
            });
        }

        /// <summary>读取原始书目的结构化作者、出版商和日期；缺失 ISBN 或日期部分保持缺失。</summary>
        public BookMetadata BookDetails(CatalogRecord source=null)
        {
            source=source??Book();if(source.Kind!="book")throw new ArgumentException("需要图书目录记录。",nameof(source));
            _c.CancellationToken.ThrowIfCancellationRequested();var raw=JObject.Parse(source.PayloadJson);
            var authorArray=raw["author"] as JArray??new JArray();
            var titleArray=raw["title"] as JArray??new JArray();var isbnArray=raw["ISBN"] as JArray??new JArray();
            if((long)authorArray.Count+titleArray.Count+isbnArray.Count>_c.Limits.MaxCollectionSize)throw new MockLimitException(nameof(MockLimits.MaxCollectionSize),_c.Limits.MaxCollectionSize);
            var authors=authorArray.Select(a=>
            {
                _c.CancellationToken.ThrowIfCancellationRequested();string given=(string)a["given"],family=(string)a["family"];
                return new BookAuthor{Given=given==null?null:Text(given),Family=family==null?null:Text(family),Orcid=a["ORCID"]==null?null:Text((string)a["ORCID"]),Name=Text((string)a["name"]??string.Join(" ",new[]{given,family}.Where(n=>!string.IsNullOrEmpty(n))))};
            }).ToArray();
            var date=raw["published"]?["date-parts"]?.First as JArray;
            string publisher=(string)raw["publisher"];
            return new BookMetadata{Source=source,Doi=raw["DOI"]==null?null:Text((string)raw["DOI"]),Titles=Array.AsReadOnly(titleArray.Select(t=>Text((string)t)).ToArray()),Isbns=Array.AsReadOnly(isbnArray.Select(t=>Text((string)t)).ToArray()),Authors=Array.AsReadOnly(authors),Publisher=publisher==null?null:new BookPublisher{Name=Text(publisher)},PublicationYear=date?.Count>0?(int?)date[0]:null,PublicationMonth=date?.Count>1?(int?)date[1]:null,PublicationDay=date?.Count>2?(int?)date[2]:null};
        }
    }
}
