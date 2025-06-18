//Copyright Warren Harding 2025.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RulesData;
using RulesData.Generators;
using System;

namespace RulesDataTest
{
    [TestClass]
    public sealed class DataTypeGeneratorFactoryTests
    {
        [TestMethod]
        public void GetGenerator_String_ReturnsStringDataTypeGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("string");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(StringDataTypeGenerator));
            Assert.IsInstanceOfType(generator.Generate(), typeof(string));
        }

        [TestMethod]
        public void GetGenerator_Number_ReturnsDecimalDataTypeGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("number");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(DoubleDataTypeGenerator));
            Assert.IsInstanceOfType(generator.Generate(), typeof(double));
        }

        [TestMethod]
        public void GetGenerator_Integer_ReturnsIntegerDataTypeGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("integer");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(IntegerDataTypeGenerator));
            Assert.IsInstanceOfType(generator.Generate(), typeof(int));
        }

        [TestMethod]
        public void GetGenerator_Boolean_ReturnsBooleanDataTypeGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("boolean");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(BooleanDataTypeGenerator));
            Assert.IsInstanceOfType(generator.Generate(), typeof(bool));
        }

        [TestMethod]
        public void GetGenerator_Date_ReturnsDateTimeDataTypeGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("date");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(DateTimeDataTypeGenerator));
            Assert.IsInstanceOfType(generator.Generate(), typeof(DateTime));
        }

        [TestMethod]
        public void GetGenerator_DateTime_ReturnsDateTimeDataTypeGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("dateTime");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(DateTimeDataTypeGenerator));
            Assert.IsInstanceOfType(generator.Generate(), typeof(DateTime));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void GetGenerator_NullEmptyOrWhitespaceTypeRef_ReturnsStringDataTypeGenerator(string? typeRef)
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator(typeRef!);
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(StringDataTypeGenerator));
        }

        [TestMethod]
        public void GetGenerator_TypeRefContainsHash_ReturnsCorrectGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("http://example.com/dmn#number");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(DoubleDataTypeGenerator));

            generator = DataTypeGeneratorFactory.GetGenerator("urn:DMN:FEEL#string");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(StringDataTypeGenerator));
        }

        [TestMethod]
        public void GetGenerator_UnrecognizedTypeRef_ReturnsStringDataTypeGenerator()
        {
            IDataTypeGenerator? generator = DataTypeGeneratorFactory.GetGenerator("unrecognizedType");
            Assert.IsNotNull(generator);
            Assert.IsInstanceOfType(generator, typeof(StringDataTypeGenerator));
        }
    }
}