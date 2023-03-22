using System;
using System.Reflection;
using Object = UnityEngine.Object;


namespace HM.Editor.HMAddressable.Editor
{
    public static class MyEditorTools
    {

        public static void SetPrivateField(Type typeOrBaseType, object instance, string fieldName, object value)
        {
            var field = typeOrBaseType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic|BindingFlags.Public);
       
           field.SetValue(instance,value);
        }
        public static T GetPrivateField<T>(Type typeOrBaseType,object instance, string fieldName)
        {
            var field = typeOrBaseType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic|BindingFlags.Public);
       
           return (T)field.GetValue(instance) ;
        }

        public static void SetStaticPrivateField(Type type,string fieldName,object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic|BindingFlags.Public);
       
            field.SetValue(type,value);
        }
        public static T GetStaticPrivateField<T>(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic|BindingFlags.Public);
       
          return  (T)field.GetValue(type);
        }

        /// <summary>
        /// 注意属性是否拥有set,如果没有set也不可以
        /// </summary>
        /// <param name="type"></param>
        /// <param name="instance"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public static void SetPrivateProperty(Type typeOrBaseType,object instance, string propertyName, object value)
        {
            var field= typeOrBaseType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic|BindingFlags.Public);
            field.SetValue(instance,value);
        }
    
        public static T GetPrivateProterty<T>(Type typeOrBaseType,object instance, string propertyName)
        {
            var field= typeOrBaseType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic|BindingFlags.Public);
           return (T)field.GetValue(instance);
        }
        
        /// <summary>
        /// 注意属性是否拥有set,如果没有set也不可以
        /// </summary>
        /// <param name="type"></param>
        /// <param name="instance"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public static void SetStaticPrivateProperty(Type type, string propertyName, object value)
        {
            var field= type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.NonPublic|BindingFlags.Public);
            field.SetValue(type,value);
        }
    
        public static T GetStaticPrivateProterty<T>(Type type, string propertyName)
        {
            var field= type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.NonPublic|BindingFlags.Public);
            return (T)field.GetValue(type);
        }
        
        
        public static void CallPrivateMethod(Type typeOrBaseType,object instance, string fieldName,params object[] paramsVlues)
        {
            var field=typeOrBaseType.GetMethod(fieldName, BindingFlags.Instance | BindingFlags.NonPublic|BindingFlags.Public);
            field.Invoke(instance, paramsVlues);
        }

        public static T CallPrivateMethodWithReturn<T>(Type typeOrBaseType,object instance, string fieldName,params object[] paramsVlues)
        {
            var field= typeOrBaseType.GetMethod(fieldName, BindingFlags.Instance | BindingFlags.NonPublic|BindingFlags.Public);
           return (T)field.Invoke(instance, paramsVlues);
        }
        
        public static void CallStaticPrivateMethod(Type type, string fieldName,params object[] paramsVlues)
        {
            var field= type.GetMethod(fieldName, BindingFlags.Static | BindingFlags.NonPublic|BindingFlags.Public);
            field.Invoke(type, paramsVlues);
        }

        public static T CallStaticPrivateMethodWithReturn<T>(Type type, string fieldName,params object[] paramsVlues)
        {
            var field= type.GetMethod(fieldName, BindingFlags.Static | BindingFlags.NonPublic|BindingFlags.Public);
            return (T)field.Invoke(type, paramsVlues);
        }

       
        
        
    }
}