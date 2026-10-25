using System.Collections.Generic;
using Bing.MockData.Catalogs;

namespace Bing.MockData.Education
{
    /// <summary>模拟专业定义，不表示某真实机构开设该专业。</summary>
    public sealed class Major
    {
        /// <summary>专业代码。</summary>
        public string Code { get; set; }
        /// <summary>专业名称。</summary>
        public string Name { get; set; }
    }
    /// <summary>属于指定机构及专业的模拟班级。</summary>
    public sealed class AcademicClass
    {
        /// <summary>班级代码。</summary>
        public string Code { get; set; }
        /// <summary>真实机构目录引用。</summary>
        public CatalogRecord Institution { get; set; }
        /// <summary>模拟专业引用。</summary>
        public Major Major { get; set; }
        /// <summary>入学年份。</summary>
        public int EntryYear { get; set; }
    }
    public sealed partial class Course
    {
        /// <summary>结构化专业，与 Major 名称一致。</summary>
        public Major MajorDefinition { get; set; }
    }
    public sealed partial class Student
    {
        /// <summary>结构化班级引用，与 Class、Institution、Major 一致。</summary>
        public AcademicClass AcademicClass { get; set; }
    }
    /// <summary>有界的同专业教学场景；所有学生、课程和选课引用均在场景内。</summary>
    public sealed class EducationCohort
    {
        /// <summary>真实机构目录引用。</summary>
        public CatalogRecord Institution { get; set; }
        /// <summary>模拟专业。</summary>
        public Major Major { get; set; }
        /// <summary>模拟班级。</summary>
        public IReadOnlyList<AcademicClass> Classes { get; set; }
        /// <summary>模拟课程。</summary>
        public IReadOnlyList<Course> Courses { get; set; }
        /// <summary>模拟学生。</summary>
        public IReadOnlyList<Student> Students { get; set; }
        /// <summary>选课及成绩。</summary>
        public IReadOnlyList<Enrollment> Enrollments { get; set; }
    }
    /// <summary>来源提供的作者信息，缺值保持为空，不自动编造作者。</summary>
    public sealed class BookAuthor
    {
        /// <summary>来源名。</summary>
        public string Given { get; internal set; }
        /// <summary>来源姓。</summary>
        public string Family { get; internal set; }
        /// <summary>来源组织作者名，或由来源姓名组合的展示值。</summary>
        public string Name { get; internal set; }
        /// <summary>来源 ORCID；缺失时为空。</summary>
        public string Orcid { get; internal set; }
    }
    /// <summary>书目中提供的出版商。</summary>
    public sealed class BookPublisher
    {
        /// <summary>来源原始名称。</summary>
        public string Name { get; internal set; }
    }
    /// <summary>结构化原始书目，ISBN 和日期只保留来源已提供的精度。</summary>
    public sealed class BookMetadata
    {
        /// <summary>可追溯的原始目录记录。</summary>
        public CatalogRecord Source { get; internal set; }
        /// <summary>来源 DOI。</summary>
        public string Doi { get; internal set; }
        /// <summary>来源标题集合。</summary>
        public IReadOnlyList<string> Titles { get; internal set; }
        /// <summary>来源 ISBN 集合，缺失时为空集合。</summary>
        public IReadOnlyList<string> Isbns { get; internal set; }
        /// <summary>来源作者集合，缺失时为空集合。</summary>
        public IReadOnlyList<BookAuthor> Authors { get; internal set; }
        /// <summary>来源出版商，缺失时为空。</summary>
        public BookPublisher Publisher { get; internal set; }
        /// <summary>出版年，来源缺失时为空。</summary>
        public int? PublicationYear { get; internal set; }
        /// <summary>出版月，来源只提供年份时为空。</summary>
        public int? PublicationMonth { get; internal set; }
        /// <summary>出版日，来源未提供时为空。</summary>
        public int? PublicationDay { get; internal set; }
    }
}
