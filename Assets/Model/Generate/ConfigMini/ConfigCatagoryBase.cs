using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExcelConfig
{
    public class ConfigCatagoryBase<T> where T : IConfig, new()
    {
        private readonly Dictionary<int, T> configMap = new Dictionary<int, T>();

        public void Init(string miniJsonText)
        {
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<MiniJsonData>(miniJsonText);
            StaticInit(obj, configMap);
        }

        public T Get(int id)
        {
            this.configMap.TryGetValue(id, out T item);

            if (item == null)
            {
                throw new Exception($"配置找不到，配置表名: {nameof(TestConfig)}，配置id: {id}");
            }

            return item;
        }

        public bool Contain(int id)
        {
            return this.configMap.ContainsKey(id);
        }

        public Dictionary<int, T> GetAll()
        {
            return this.configMap;
        }

        public T GetOne()
        {
            if (this.configMap == null || this.configMap.Count <= 0)
            {
                return default(T);
            }

            return this.configMap.Values.GetEnumerator().Current;
        }


        private static void StaticInit(MiniJsonData miniObjs, Dictionary<int, T> dict)
        {
            dict.Clear();
            Type type = typeof(T);
            PropertyInfo[] fieldInfos = new PropertyInfo[miniObjs.fieldNames.Length];
            for (var i = 0; i < miniObjs.fieldNames.Length; i++)
            {
                fieldInfos[i] = type.GetProperty(miniObjs.fieldNames[i], BindingFlags.Public | BindingFlags.Instance);
            }

            for (var i = 0; i < miniObjs.datas.Length; i++)
            {
                var datas = miniObjs.datas[i];
                T vT = new T();
                for (var j = 0; j < fieldInfos.Length; j++)
                {
                    SetValue(fieldInfos[j], vT, datas[j], miniObjs.fieldTypes[j]);
                }

                dict.Add(vT.Id, vT);
            }
        }

        private static void SetValue(PropertyInfo fieldInfo, object t, string vStr, string fieldType)
        {
            switch (fieldType)
            {
                case "int":
                    fieldInfo.SetValue(t, int.Parse(vStr));
                    return;
                case "uint":
                    fieldInfo.SetValue(t, uint.Parse(vStr));
                    return;
                case "string":
                    fieldInfo.SetValue(t, vStr);
                    return;
                case "long":
                    fieldInfo.SetValue(t, long.Parse(vStr));
                    return;
                case "double":
                    fieldInfo.SetValue(t, double.Parse(vStr));
                    return;
                case "float":
                    fieldInfo.SetValue(t, float.Parse(vStr));
                    return;
                case "bool":
                    fieldInfo.SetValue(t, bool.Parse(vStr));
                    return;

                case "int[]":
                    fieldInfo.SetValue(t, Newtonsoft.Json.JsonConvert.DeserializeObject<List<int>>(vStr));
                    return;
                case "uint[]":
                    fieldInfo.SetValue(t, Newtonsoft.Json.JsonConvert.DeserializeObject<List<uint>>(vStr));
                    return;
                case "string[]":
                    fieldInfo.SetValue(t, Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(vStr));
                    return;
                case "long[]":
                    fieldInfo.SetValue(t, Newtonsoft.Json.JsonConvert.DeserializeObject<List<long>>(vStr));
                    return;
                case "double[]":
                    fieldInfo.SetValue(t, Newtonsoft.Json.JsonConvert.DeserializeObject<List<double>>(vStr));
                    return;
                case "float[]":
                    fieldInfo.SetValue(t, Newtonsoft.Json.JsonConvert.DeserializeObject<List<float>>(vStr));
                    return;
                case "bool[]":
                    fieldInfo.SetValue(t, Newtonsoft.Json.JsonConvert.DeserializeObject<List<bool>>(vStr));
                    return;
            }
        }
    }

    public class MiniJsonData
    {
        public string[] fieldNames;
        public string[] fieldTypes;
        public string[][] datas;
    }

    public interface IConfig
    {
        ///<summary>I大小,必须有Id</summary>
        public int Id { get; set; }
    }
}