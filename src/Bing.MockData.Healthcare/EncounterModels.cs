using System;
using System.Collections.Generic;
using Bing.MockData.Catalogs;

namespace Bing.MockData.Healthcare
{
    /// <summary>模拟检查项目定义；代码不代表真实诊疗标准或参考区间。</summary>
    public sealed class ExaminationItem
    {
        /// <summary>项目代码。</summary>
        public string Code { get; set; }
        /// <summary>项目名称。</summary>
        public string Name { get; set; }
        /// <summary>结果单位。</summary>
        public string Unit { get; set; }
        /// <summary>模拟结果下限。</summary>
        public double Minimum { get; set; }
        /// <summary>模拟结果上限。</summary>
        public double Maximum { get; set; }
    }
    /// <summary>一次检查的申请、采样和出结果事件。</summary>
    public sealed class Examination
    {
        /// <summary>场景内检查编号。</summary>
        public string Id { get; set; }
        /// <summary>关联检查项目。</summary>
        public ExaminationItem Item { get; set; }
        /// <summary>申请时间。</summary>
        public DateTimeOffset OrderedAt { get; set; }
        /// <summary>采样时间。</summary>
        public DateTimeOffset CollectedAt { get; set; }
        /// <summary>报告时间。</summary>
        public DateTimeOffset ResultedAt { get; set; }
        /// <summary>模拟数值结果。</summary>
        public double Result { get; set; }
        /// <summary>结果始终为模拟值。</summary>
        public bool IsSimulated => true;
    }
    /// <summary>就诊中的药品目录引用；不推导诊断与药品的适应关系。</summary>
    public sealed class MedicationReference
    {
        /// <summary>来自 RxNorm CPC 的原始目录记录。</summary>
        public CatalogRecord Medication { get; set; }
        /// <summary>药品目录快照版本。</summary>
        public string DataVersion { get; set; }
        /// <summary>模拟记录时间。</summary>
        public DateTimeOffset RecordedAt { get; set; }
        /// <summary>没有经验证的适应关系。</summary>
        public bool HasVerifiedIndication => false;
    }
    /// <summary>就诊场景配置，在创建模板时复制。</summary>
    public sealed class EncounterOptions
    {
        /// <summary>检查数量，默认两个。</summary>
        public int Examinations { get; set; } = 2;
        /// <summary>药品引用数量；设置为零时无需挂载药品目录。</summary>
        public int Medications { get; set; } = 1;
        /// <summary>可选检查项目集合，空引用使用内置模拟项目。</summary>
        public IReadOnlyList<ExaminationItem> Items { get; set; }
    }
    public sealed partial class Encounter
    {
        /// <summary>与本次就诊时间一致的检查记录。</summary>
        public IReadOnlyList<Examination> Examinations { get; set; }
        /// <summary>本次就诊的模拟药品引用。</summary>
        public IReadOnlyList<MedicationReference> Medications { get; set; }
        /// <summary>诊断代码所属快照版本。</summary>
        public string DiagnosisDataVersion { get; set; }
    }
}
