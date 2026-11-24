namespace Xbc.ImportExport.Tests
{
    public class ExceptionsTests
    {
        [Fact]
        public void ImportException_DomainConstructorWithInner_SetsInnerException()
        {
            var inner = new InvalidOperationException("inner");
            var ex = new ImportException("outer", inner);

            Assert.Equal("outer", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void ImportException_InheritsFromImportExportException()
        {
            var ex = new ImportException("msg");

            Assert.IsAssignableFrom<ImportExportException>(ex);
        }

        [Fact]
        public void ImportDataException_RowNumberOnlyConstructor_DefaultsColumnNameAndInner()
        {
            var inner = new InvalidOperationException("inner");
            var ex = new ImportDataException("outer", 3, innerException: inner);

            Assert.Equal("outer", ex.Message);
            Assert.Equal(3, ex.RowNumber);
            Assert.Equal(string.Empty, ex.ColumnName);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void ImportDataException_DomainConstructor_SetsRowNumberAndColumnName()
        {
            var inner = new InvalidOperationException("inner");
            var ex = new ImportDataException("msg", 5, "设备编码", inner);

            Assert.Equal("msg", ex.Message);
            Assert.Equal(5, ex.RowNumber);
            Assert.Equal("设备编码", ex.ColumnName);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void ImportDataException_NullColumnName_DefaultsToEmptyString()
        {
            var ex = new ImportDataException("msg", 5, null);

            Assert.Equal(string.Empty, ex.ColumnName);
        }

        [Fact]
        public void ImportDataException_InheritsFromImportException()
        {
            var ex = new ImportDataException("msg", 1);

            Assert.IsAssignableFrom<ImportException>(ex);
        }

        [Fact]
        public void ExportException_DomainConstructorWithInner_SetsInnerException()
        {
            var inner = new InvalidOperationException("inner");
            var ex = new ExportException("outer", inner);

            Assert.Equal("outer", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void ExportException_InheritsFromImportExportException()
        {
            var ex = new ExportException("msg");

            Assert.IsAssignableFrom<ImportExportException>(ex);
        }

        [Fact]
        public void TemplateException_InheritsFromImportExportException()
        {
            var ex = new TemplateException("msg");

            Assert.IsAssignableFrom<ImportExportException>(ex);
        }
    }
}
