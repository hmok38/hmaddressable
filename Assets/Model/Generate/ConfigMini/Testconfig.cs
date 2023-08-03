using System.Collections.Generic;

namespace ExcelConfig
{
    public partial class TestConfigCategory : ConfigCatagoryBase<TestConfig>
    {
        private static TestConfigCategory instance;

        public static TestConfigCategory Instance
        {
            get
            {
                if (instance==null)
                {
                    instance = new TestConfigCategory();
                }

                return instance;
            }
        }
    }

    /// <summary> 测试表 </summary>
    public class TestConfig : IConfig
    {
        ///<summary>I大小,必须有Id</summary>
        public int Id { get; set; }

        ///<summary>字符串值</summary>
        public string stringV { get; set; }

        ///<summary>整数值（数值超过范围不会溢出）</summary>
        public int intV { get; set; }

        ///<summary>整数值（数值超过范围不会溢出）</summary>
        public uint unitV { get; set; }

        ///<summary>长整形（数值超过范围不会溢出）</summary>
        public long longV { get; set; }

        ///<summary>double值都会变成float值</summary>
        public double doubleV { get; set; }

        ///<summary>float值</summary>
        public float floatV { get; set; }

        ///<summary>bool值</summary>
        public bool boolV { get; set; }

        ///<summary>字符串列表</summary>
        public List<string> stringListV { get; set; }

        ///<summary>int值列表</summary>
        public List<int> intListV { get; set; }

        ///<summary>double列表会变成float列表</summary>
        public List<double> doubleListV { get; set; }

        ///<summary>float列表</summary>
        public List<float> floatListV { get; set; }

        ///<summary>bool列表</summary>
        public List<bool> boolListV { get; set; }

    }
}