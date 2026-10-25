using System;

namespace Bing.MockData.Catalogs
{
    /// <summary>Thrown when a requested value is outside the declared coverage of a dataset.</summary>
    public sealed class CatalogCoverageException : InvalidOperationException
    {
        public string DataSetId { get; }
        public string RequestedValue { get; }

        public CatalogCoverageException(string dataSetId, string requestedValue)
            : base($"数据集 {dataSetId} 不覆盖请求值 {requestedValue}。")
        {
            DataSetId = dataSetId;
            RequestedValue = requestedValue;
        }
    }
}
