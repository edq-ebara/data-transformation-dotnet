using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utils
{
    /// <summary>
    /// Json帮助类
    /// 支持内容如下：
    /// 源属性=》目标属性
    /// 源属性=》目标对象
    /// 源属性=》目标数组
    /// 源属性=》目标数组指定元素
    /// 源对象=》目标属性
    /// 源对象=》目标对象
    /// 源对象=》目标数组
    /// 源对象=》目标数组指定元素
    /// </summary>
    public class JsonTranferUtil
    {
        #region 成员属性变量

        private string Json_Path_Regex = @"^[\d\w][\d\w\\_]*((\[([\w\d\\_]+|\*)\])*|((\.\*)|(\.[\d\w][\d\w\\_]*))*)*$";


        private string orgTemplate;

        public string OrgTemplate
        {
            get { return orgTemplate; }
            set { orgTemplate = value; }
        }

        private string aimTemplate;

        public string AimTemplate
        {
            get { return aimTemplate; }
            set { aimTemplate = value; }
        }

        private List<JsonMapping> jsonMappings = new List<JsonMapping>();

        public List<JsonMapping> JsonMappings
        {
            get { return jsonMappings; }
            set { jsonMappings = value; }
        }





        #endregion


        #region 成员构造函数

        public JsonTranferUtil(string orgTemplate, string aimTemplate, List<JsonMapping> jsonMappings)
        {
            if (string.IsNullOrEmpty(orgTemplate) || string.IsNullOrEmpty(aimTemplate) || jsonMappings == null || jsonMappings.Count <= 0)
            {
                throw new Exception("源模板、目标模板、映射关系不能为空！");
            }


            this.orgTemplate = $@"{{""root"": {orgTemplate} }}";
            this.aimTemplate = $@"{{""root"": {aimTemplate} }}";

             jsonMappings.Sort((a, b) => {
                if (a.OrgJsonPath != b.OrgJsonPath)
                {
                    return a.OrgJsonPath.GetHashCode()- b.OrgJsonPath.GetHashCode();
                }
                else if (a.OrgJsonPath == b.OrgJsonPath)
                {
                    if (a.TranType == 1 || a.TranType == 3)
                    {
                        return -1;
                    }
                    if (b.TranType == 1 || b.TranType == 3)
                    {
                        return 1;
                    }
                    return a.TranType - b.TranType;
                }
                return 0;
            });

            this.jsonMappings = JsonConvert.DeserializeObject<List<JsonMapping>>( JsonConvert.SerializeObject( jsonMappings));
        }

        #endregion

        #region 成员私有方法

        /// <summary>
        /// 加载Json的属性
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="keyList"></param>
        private void LoadJsonProperties(JToken parent, List<JsonProperty> keyList)
        {

            if (parent.Children().Count() > 0)
            {
                foreach (JToken item in parent.Children())
                {
                    if (item is JValue)
                    {
                        //var vItem = item as JValue;
                        //keyList.Add(new
                        //{
                        //    Path = vItem.Path,
                        //    Type = vItem.Type,
                        //    Value = vItem.Value,
                        //});
                    }

                    if (item is JProperty)
                    {
                        var pItem = item as JProperty;

                        string pPath = Regex.Replace(pItem.Path, @"\[.*?\]", "[*]");
                        if (keyList.FirstOrDefault(k => k.Name == pItem.Name && k.Path == pPath) == null)
                        {
                            keyList.Add(new JsonProperty
                            {
                                Path = pPath,
                                Type = pItem.Value.Type,
                                Name = pItem.Name,
                                Value = pItem.Value,
                            });
                        }
                    }



                    LoadJsonProperties(item, keyList);

                }

            }
        }

        /// <summary>
        /// Json转换
        /// </summary>
        /// <param name="orgJson">源Json</param>
        /// <param name="aimJson">目标Json</param>
        /// <param name="mapping">映射</param>
        /// <returns></returns>
        public void TranJson_Inner(JToken jOrg, JToken jAim, JToken jAim_Cur, List<JsonMapping> mapping)
        {
            if (jAim_Cur is JProperty)
            {
                var pItem = jAim_Cur as JProperty;
                var pItemPath = pItem.Path;
                var jAim_Cur_MappingList = mapping.Where(d => d.AimJsonPath == pItem.Path).ToList();

                foreach (var mappingItem in jAim_Cur_MappingList)
                {
                    var orgJsonToken = jOrg.SelectToken(mappingItem.OrgJsonPath).Ancestors().FirstOrDefault(c => c.Type == JTokenType.Property);
                    if (orgJsonToken is JProperty)
                    {
                        var orgJsonTokenProperty = orgJsonToken as JProperty;
                        if (orgJsonTokenProperty != null)
                        {
                            var pItemParent = pItem.Parent;

                            /// 转换类型
                            /// 1：源Key->目标Key
                            /// 2：源Key->目标Value
                            /// 3：源Value->目标Key
                            /// 4：源Value->目标Value
                            switch (mappingItem.TranType)
                            {
                                case 1:
                                    pItem.Remove();
                                    var newPItemKey = new JProperty(orgJsonTokenProperty.Name, pItem.Value);
                                    pItemParent.AddFirst(newPItemKey);
                                    var relMappings1 = mapping.Where(m => Regex.Match(m.AimJsonPath, @$"{ pItemPath.Replace("[", "\\[").Replace("]", "\\]")}").Success).ToList();
                                    relMappings1.ForEach(item =>
                                    {
                                        item.AimJsonPath = item.AimJsonPath.Replace($"{pItemPath}", newPItemKey.Path);
                                    });
                                    jAim_Cur = pItem = newPItemKey;
                                    break;
                                case 2:
                                    pItem.Value = orgJsonTokenProperty.Name;
                                    break;
                                case 3:

                                    pItem.Remove();
                                    var newPItemValue = new JProperty(orgJsonTokenProperty.Value.ToString(), pItem.Value);
                                    pItemParent.AddFirst(newPItemValue);

                                    var relMappings = mapping.Where(m => Regex.Match(m.AimJsonPath, @$"{ pItemPath.Replace("[", "\\[").Replace("]", "\\]")}").Success).ToList();
                                    relMappings.ForEach(item =>
                                    {
                                        item.AimJsonPath = item.AimJsonPath.Replace($"{pItemPath}", newPItemValue.Path);
                                    });
                                    jAim_Cur = pItem = newPItemValue;
                                    break;
                                case 4:
                                    pItem.Value = orgJsonTokenProperty.Value;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }

            }



            for (int jAim_CurIndex = 0; jAim_CurIndex < jAim_Cur.Children().Count(); jAim_CurIndex++)
            {
                JToken jAim_CurChild = jAim_Cur.Children().ToList()[jAim_CurIndex];

                TranJson_Inner(jOrg, jAim, jAim_CurChild, mapping);
            }
        }

        /// <summary>
        /// 构建Mapping
        /// </summary>
        /// <param name="jOrg"></param>
        /// <param name="jAim"></param>
        /// <param name="jAim_Cur"></param>
        /// <param name="mappings"></param>
        /// <returns></returns>
        public void BuildJsonMapping(JToken jOrg, JToken jAim, JToken jAim_Cur, List<JsonMapping> mappings)
        {
            //忽略不处理
            if (!(jAim_Cur is JProperty))
            {
                //Console.WriteLine($"非JProperty ProP  Type:{jAim_Cur.Type}    Path:{jAim_Cur.Path}    ParentType:{jAim_Cur.Parent?.Type}     ParentPath:{jAim_Cur.Parent?.Path} ");

            }
            //只处理属性及其值
            if ((jAim_Cur is JProperty))
            {
                //将JToken强转成JProperty
                var jAim_CurProp = jAim_Cur as JProperty;
                //当前目标属性的JValue
                var jAim_CurValue = jAim_CurProp.Value;

                //Console.WriteLine($"JProperty ProP  Type:{jAim_Cur.Type}    Path:{jAim_Cur.Path}    ParentType:{jAim_Cur.Parent?.Type}     ParentPath:{jAim_Cur.Parent?.Path} ");

                //Console.WriteLine($"JProperty Value  Type:{jAim_CurValue.Type}    Path:{jAim_CurValue.Path}    ParentType:{jAim_CurValue.Parent?.Type}     ParentPath:{jAim_CurValue.Parent?.Path} ");



                JsonMapping mappingItem = null;
                List<JsonMapping> childMappings = new List<JsonMapping>();
                switch (jAim_CurValue.Type)
                {
                    //目标属性是个对象
                    case JTokenType.Object:

                        //【* =》对象】的情况下，映射类型只能是值到值【 TranType=4】
                        mappingItem = mappings.Where(d => d.AimJsonPath == jAim_CurValue.Path && d.TranType == 4).FirstOrDefault();

                        if (mappingItem != null)
                        {
                            //依据当前映射获取源的JProperty
                            var jOrg_CurProp = jOrg.SelectTokens(mappingItem.OrgJsonPath).FirstOrDefault()?.Ancestors().Where(t => t.Type == JTokenType.Property).FirstOrDefault() as JProperty;
                            if (jOrg_CurProp != null)
                            {
                                //当前源属性的JValue
                                var jOrg_CurValue = jOrg_CurProp.Value;

                                switch (jOrg_CurValue.Type)
                                {
                                    //源属性是个对象
                                    case JTokenType.Object:

                                        childMappings = mappings.Where(m => Regex.Match(m.AimJsonPath, @$"^{jAim_CurValue.Path}\.\w*$").Success).ToList();


                                        //判断【对象 =》对象】的情况下，映射关系里是否包含目标对象属性的映射，如果包含说明客户端已经进行了指定在此不做默认处理，如果不存在则按默认操作将目标对象和源对象按照属性名和TransType 进行映射
                                        if (childMappings.Count <= 0)
                                        {
                                            //轮询所有目标对象的所有属性构建与源对象同属性名的映射
                                            foreach (var jAim_Cur_Child in jAim_CurValue.Children().ToList())
                                            {
                                                if ((jAim_Cur_Child is JProperty))
                                                {
                                                    var jAim_Cur_ChildProp = jAim_Cur_Child as JProperty;

                                                    var jOrg_Cur_ChildPropList = new List<JProperty>();
                                                    foreach (var jOrg_Cur_Child in jOrg_CurValue.Children())
                                                    {
                                                        if (jOrg_Cur_Child is JProperty)
                                                        {
                                                            jOrg_Cur_ChildPropList.Add(jOrg_Cur_Child as JProperty);
                                                        }
                                                    }

                                                    var jOrg_Cur_ChildProp = jOrg_Cur_ChildPropList.FirstOrDefault(c => c.Name.ToLower() == jAim_Cur_ChildProp.Name.ToLower());

                                                    if (jOrg_Cur_ChildProp != null)
                                                    {
                                                        mappings.Add(new JsonMapping()
                                                        {
                                                            AimJsonPath = jAim_Cur_ChildProp.Path,
                                                            OrgJsonPath = jOrg_Cur_ChildProp.Path,
                                                            TranType = mappingItem.TranType
                                                        });
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    case JTokenType.Array:

                                        childMappings = mappings.Where(m => Regex.Match(m.AimJsonPath, @$"^{jAim_CurValue.Path}\.\w*$").Success && (m.TranType == 3 || m.TranType == 4)).ToList();


                                        //判断【数组 =》对象】的情况下，映射关系里是否包含目标对象属性的映射且转换类型是【3：源Value->目标Key或4：源Value->目标Value】，如果包含说明客户端已经进行了指定在此需进一步处理，如果不存在则将此【数组=》对象】的映射关系删掉，因为系统不知如何将其转换为对象
                                        if (childMappings.Count() <= 0)
                                        {
                                            mappings.Remove(mappingItem);
                                        }
                                        else
                                        {

                                            JArray jOrgArray = jOrg.SelectToken(jOrg_CurValue.Path) as JArray;

                                            if (jOrgArray != null)
                                            {
                                                childMappings.ForEach(item =>
                                                {
                                                    mappings.Remove(item);
                                                });
                                                for (int i = 0; i < childMappings.Count; i++)
                                                {
                                                    var childMapping = childMappings[i];

                                                    var siblingMappings1 = mappings.Where(m => m.AimJsonPath.EndsWith($"{childMapping.AimJsonPath}")).ToList();
                                                    siblingMappings1.ForEach(item =>
                                                    {
                                                        mappings.Remove(item);
                                                    });

                                                    var childMappings1 = mappings.Where(m => m.AimJsonPath.Contains($"{childMapping.AimJsonPath}.")).ToList();
                                                    childMappings1.ForEach(item =>
                                                    {
                                                        mappings.Remove(item);
                                                    });

                                                    for (int j = 0; j < jOrgArray.Count; j++)
                                                    {
                                                        var newMapping = new JsonMapping()
                                                        {
                                                            AimJsonPath = $"{childMapping.AimJsonPath}{j}",
                                                            OrgJsonPath = childMapping.OrgJsonPath.Replace("[*]", $"[{j}]"),
                                                            TranType = childMapping.TranType
                                                        };

                                                        siblingMappings1.ForEach(item =>
                                                        {
                                                            var newMappingChild = new JsonMapping()
                                                            {
                                                                AimJsonPath = item.AimJsonPath.Replace($"{childMapping.AimJsonPath}", $"{newMapping.AimJsonPath}"),
                                                                OrgJsonPath = Regex.Match(item.OrgJsonPath, @$"^{jOrg_CurValue.Path}\[\*\]\..*$").Success ? item.OrgJsonPath.Replace($"{jOrg_CurValue.Path}[*].", $"{jOrg_CurValue.Path}[{j}].") : item.OrgJsonPath,
                                                                TranType = item.TranType
                                                            };
                                                            mappings.Add(newMappingChild);
                                                        });

                                                        childMappings1.ForEach(item =>
                                                        {
                                                            var newMappingChild = new JsonMapping()
                                                            {
                                                                AimJsonPath = item.AimJsonPath.Replace($"{childMapping.AimJsonPath}.", $"{newMapping.AimJsonPath}."),
                                                                OrgJsonPath = Regex.Match(item.OrgJsonPath, @$"^{jOrg_CurValue.Path}\[\*\]\..*$").Success ? item.OrgJsonPath.Replace($"{jOrg_CurValue.Path}[*].", $"{jOrg_CurValue.Path}[{j}].") : item.OrgJsonPath,
                                                                TranType = item.TranType
                                                            };
                                                            mappings.Add(newMappingChild);
                                                        });



                                                        mappings.Add(newMapping);
                                                    }
                                                }

                                                var jAim_CurObject = jAim.SelectToken(jAim_CurValue.Path) as JObject;
                                                if (jAim_CurObject != null)
                                                {
                                                    JProperty jAim_CurChildProp = null;
                                                    foreach (var item in jAim_CurObject.Children())
                                                    {
                                                        if (item is JProperty)
                                                        {
                                                            jAim_CurChildProp = item as JProperty;
                                                            break;
                                                        }
                                                    }

                                                    jAim_CurObject.RemoveAll();
                                                    for (int j = 0; j < jOrgArray.Count; j++)
                                                    {
                                                        jAim_CurObject.Add($"{jAim_CurChildProp.Name}{j}", jAim_CurChildProp.Value);
                                                    }
                                                }
                                            }

                                        }
                                        break;

                                    default:
                                        //判断【非对象和数组 =》对象】的情况下，映射关系里是否包含目标对象属性的映射，如果包含说明客户端已经进行了指定在此不做默认处理，如果不存在则将此【非对象=》对象】的映射关系删掉，因为系统不知如何将其转换为对象
                                        if (mappings.Count(m => m.AimJsonPath.Contains($"{jAim_CurValue.Path}.")) <= 0)
                                        {
                                            mappings.Remove(mappingItem);
                                        }
                                        break;
                                }
                            }

                            //删除当前的映射
                            mappings.Remove(mappingItem);
                        }
                        break;
                    //目标属性是个数组
                    case JTokenType.Array:
                        //【* =》数组】的情况下，映射类型只能是值到值【 TranType=4】
                        mappingItem = mappings.Where(d => d.AimJsonPath == jAim_CurValue.Path && d.TranType == 4).FirstOrDefault();

                        if (mappingItem != null)
                        {
                            //依据当前映射获取源的JProperty
                            var jOrg_CurProp = jOrg.SelectTokens(mappingItem.OrgJsonPath).FirstOrDefault()?.Ancestors().Where(t => t.Type == JTokenType.Property).FirstOrDefault() as JProperty;
                            if (jOrg_CurProp != null)
                            {
                                //当前源属性的JValue
                                var jOrg_CurValue = jOrg_CurProp.Value;

                                switch (jOrg_CurValue.Type)
                                {
                                    //源属性是个对象
                                    case JTokenType.Object:

                                        childMappings = mappings.Where(m => Regex.Match(m.AimJsonPath, @$"{jAim_CurValue.Path.Replace("[", "\\[").Replace("]", "\\]")}\[.*?\]\.\w*").Success).ToList();

                                        //判断【对象 =》数组】的情况下，映射关系里是否包含目标数组属性的映射，如果包含说明客户端已经进行了指定,如果在指定的映射中源路径包含类似【a.*】则需要进一步处理，否则不做默认处理，如果不存在则将此【对象=》数组】的映射关系删掉，因为系统不知如何将其转换为数组
                                        if (childMappings.Count <= 0)
                                        {
                                            mappings.Remove(mappingItem);
                                        }
                                        else
                                        {
                                            var childMappingList = childMappings.Where(m => Regex.Match(m.OrgJsonPath, @$"^{jOrg_CurValue.Path}\.\*").Success).ToList();
                                            foreach (var item in childMappingList)
                                            {
                                                mappings.Remove(item);
                                            }

                                            var jAimArray = jAim.SelectToken(jAim_CurValue.Path) as JArray;
                                            var jAim_CurChildProp = jAimArray.Children().FirstOrDefault();

                                            jAimArray.RemoveAll();

                                            var childOrgList = jOrg_CurValue.Children();
                                            for (int i = 0; i < childOrgList.Count(); i++)
                                            {
                                                if (childOrgList.ElementAt(i) is JProperty)
                                                {
                                                    foreach (var childMapping in childMappings)
                                                    {
                                                        var curChildOrg = childOrgList.ElementAt(i) as JProperty;
                                                        mappings.Add(new JsonMapping()
                                                        {
                                                            AimJsonPath = childMapping.AimJsonPath.Replace($"{jAim_CurValue.Path}[*]", $"{jAim_CurValue.Path}[{i}]"),
                                                            OrgJsonPath = childMapping.OrgJsonPath.Replace($"{jOrg_CurValue.Path}.*", curChildOrg.Path),
                                                            TranType = childMapping.TranType
                                                        });
                                                    }


                                                    jAimArray.Add(jAim_CurChildProp);
                                                }
                                            }

                                        }

                                        break;
                                    case JTokenType.Array:

                                        childMappings = mappings.Where(m => Regex.Match(m.AimJsonPath, @$"{jAim_CurValue.Path.Replace("[", "\\[").Replace("]", "\\]")}\[.*?\]\.\w*").Success).ToList();


                                        //判断【数组 =》数组】的情况下，映射关系里是否包含目标数组属性的映射，如果包含说明客户端已经进行了指定,如果进行了指定则需要进一步处理，如果不存在则将此【对象=》数组】的映射关系删掉并依据原数组元素个数将目标数组元素添加为相等数量
                                        if (childMappings.Count() <= 0)
                                        {
                                            JArray jOrgArray = jOrg.SelectToken(jOrg_CurValue.Path) as JArray;
                                            if (jOrgArray != null)
                                            {

                                                var jAim_CurArray = jAim.SelectToken(jAim_CurValue.Path) as JArray;
                                                if (jAim_CurArray != null)
                                                {
                                                    var jAim_CurChildProp = jAim_CurArray.Children().First();

                                                    jAim_CurArray.RemoveAll();
                                                    for (int j = 0; j < jOrgArray.Count; j++)
                                                    {
                                                        jAim_CurArray.Add(jAim_CurChildProp);
                                                    }
                                                }
                                            }
                                            mappings.Remove(mappingItem);
                                        }
                                        else
                                        {
                                            var childMappingForPatten = childMappings.FirstOrDefault(m => Regex.Match(m.OrgJsonPath, @$"{jOrg_CurValue.Path.Replace("[", "\\[").Replace("]", "\\]")}\[\*\]\.\w*").Success);
                                            if (childMappingForPatten != null)
                                            {
                                                JArray jOrgArray = jOrg.SelectToken(jOrg_CurValue.Path) as JArray;

                                                if (jOrgArray != null)
                                                {
                                                    for (int i = 0; i < childMappings.Count; i++)
                                                    {
                                                        var childMapping = childMappings[i];
                                                        mappings.Remove(childMapping);

                                                        for (int j = 0; j < jOrgArray.Count; j++)
                                                        {
                                                            mappings.Add(new JsonMapping()
                                                            {
                                                                AimJsonPath = childMapping.AimJsonPath.Replace($"{jAim_CurValue.Path}[*]", $"{jAim_CurValue.Path}[{j}]"),
                                                                OrgJsonPath = childMapping.OrgJsonPath.Replace($"{jOrg_CurValue.Path}[*]", $"{jOrg_CurValue.Path}[{j}]"),
                                                                TranType = childMapping.TranType
                                                            });
                                                        }
                                                    }

                                                    var jAim_CurArray = jAim.SelectToken(jAim_CurValue.Path) as JArray;
                                                    if (jAim_CurArray != null)
                                                    {
                                                        var jAim_CurChildProp = jAim_CurArray.Children().First();

                                                        jAim_CurArray.RemoveAll();
                                                        for (int j = 0; j < jOrgArray.Count; j++)
                                                        {
                                                            jAim_CurArray.Add(jAim_CurChildProp);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        break;

                                    default:
                                        //判断【非对象和数组 =》对象】的情况下，映射关系里是否包含目标对象属性的映射，如果包含说明客户端已经进行了指定在此不做默认处理，如果不存在则将此【非对象=》对象】的映射关系删掉，因为系统不知如何将其转换为对象
                                        if (mappings.Count(m => m.AimJsonPath.Contains($"{jAim_CurValue.Path}.")) <= 0)
                                        {
                                            mappings.Remove(mappingItem);
                                        }
                                        break;
                                }
                            }

                            //删除当前的映射
                            mappings.Remove(mappingItem);
                        }

                        break;
                    case JTokenType.None:
                        break;
                    case JTokenType.Constructor:
                        break;
                    case JTokenType.Property:
                        break;
                    case JTokenType.Comment:
                        break;
                    case JTokenType.Integer:
                        break;
                    case JTokenType.Float:
                        break;
                    case JTokenType.String:
                        break;
                    case JTokenType.Boolean:
                        break;
                    case JTokenType.Null:
                        break;
                    case JTokenType.Undefined:
                        break;
                    case JTokenType.Date:
                        break;
                    case JTokenType.Raw:
                        break;
                    case JTokenType.Bytes:
                        break;
                    case JTokenType.Guid:
                        break;
                    case JTokenType.Uri:
                        break;
                    case JTokenType.TimeSpan:
                        break;
                    default:
                        break;
                }

            }

            for (int jAim_CurIndex = 0; jAim_CurIndex < jAim_Cur.Children().Count(); jAim_CurIndex++)
            {
                JToken jAim_CurChild = jAim_Cur.Children().ToList()[jAim_CurIndex];

                BuildJsonMapping(jOrg, jAim, jAim_CurChild, mappings);
            }

        }

        /// <summary>
        /// 构建Mapping 初始化（仅仅只是简便类库使用者使用，自动为其补充映射）
        /// 处理数组任意子元素转对象任意或指定子元素|数组任意或指定子元素（只设置了源任意子元素到目标任意或指定子元素的映射，没有设置父元素的映射（TranType=4））
        /// 对象任意子元素转数组任意子元素（只设置了源任意子元素到目标任意子元素的映射），其它情况不考虑      
        /// </summary>
        /// <param name="mappings"></param>
        private void BuildJsonMapping_Init(List<JsonMapping> mappings)
        {

            var tempMappings = JsonConvert.DeserializeObject<List<JsonMapping>>(JsonConvert.SerializeObject(mappings));

            for (var index = 0; index < tempMappings.Count; index++)
            {
                var mappingItem = tempMappings[index];

                //源是数组情况：处理源是对象任意子元素，目标是对象任意或指定子元素|数组任意子元素或指定子元素
                var regexArr = "^[\\w|\\.|(\\[\\w\\])]+\\[\\*\\]\\.\\w*$";
                var matchsArr = Regex.Match(mappingItem.OrgJsonPath, regexArr).Success;
                if (matchsArr)
                {

                    var mappingItemPathIndex = mappingItem.OrgJsonPath.IndexOf("[*]");
                    var mappingItemIndex = tempMappings.FindIndex(m => m.OrgJsonPath == mappingItem.OrgJsonPath && m.AimJsonPath == mappingItem.AimJsonPath && m.TranType == mappingItem.TranType);
                    var orgPath = mappings[mappingItemIndex].OrgJsonPath.Substring(0, mappingItemPathIndex);
                    var orgPathTemp = mappingItem.OrgJsonPath.Substring(0, mappingItemPathIndex);

                    //var orgPath = mappingItem.OrgJsonPath.Substring(0, mappingItem.OrgJsonPath.IndexOf("[*]"));
                    var aimPath = mappingItem.AimJsonPath.Substring(0, Math.Max(mappingItem.AimJsonPath.LastIndexOf("[*]"), mappingItem.AimJsonPath.LastIndexOf(".")));


                    //生成mappings的新元素
                    var mapping = new JsonMapping
                    {
                        AimJsonPath = aimPath,
                        OrgJsonPath = orgPath,
                        TranType = 4
                    };
                    //生成tempMappings的新元素
                    var mapping_Temp = new JsonMapping
                    {
                        AimJsonPath = aimPath,
                        OrgJsonPath = orgPathTemp,
                        TranType = 4
                    };
                    var hasMapping = mappings.FirstOrDefault(m => m.AimJsonPath == mapping.AimJsonPath && m.TranType == 4) != null;
                    if (!hasMapping)
                    {
                        mappings.Insert(index, mapping);
                        tempMappings.Insert(index, mapping_Temp);
                    }

                    tempMappings.ForEach(mappingItemCur =>
                    {
                        mappingItemCur.OrgJsonPath = mappingItemCur.OrgJsonPath.Replace($"{orgPathTemp}[*]", $"{orgPathTemp}[0]");
                    });
                }

                //源是对象情况：只处理源是对象任意子元素且目标是数组任意子元素
                var regexObj = "^[\\w|\\.|(\\[\\w\\])]+\\.\\*$";
                var matchsObj = Regex.Match(mappingItem.OrgJsonPath, regexObj).Success;
                matchsArr = Regex.Match(mappingItem.AimJsonPath, regexArr).Success;
                if (matchsObj && matchsArr)
                {

                    var mappingItemPathIndex = mappingItem.OrgJsonPath.IndexOf(".*");
                    var mappingItemIndex = tempMappings.FindIndex(m => m.OrgJsonPath == mappingItem.OrgJsonPath && m.AimJsonPath == mappingItem.AimJsonPath && m.TranType == mappingItem.TranType);
                    var orgPath = mappings[mappingItemIndex].OrgJsonPath.Substring(0, mappingItemPathIndex);
                    var orgPathTemp = mappingItem.OrgJsonPath.Substring(0, mappingItemPathIndex);
                    var aimPath = mappings[mappingItemIndex].AimJsonPath.Substring(0, mappingItem.AimJsonPath.LastIndexOf("[*]"));



                    //生成mappings的新元素
                    var mapping = new JsonMapping
                    {
                        AimJsonPath = aimPath,
                        OrgJsonPath = orgPath,
                        TranType = 4
                    };
                    //生成tempMappings的新元素
                    var mapping_Temp = new JsonMapping
                    {
                        AimJsonPath = aimPath,
                        OrgJsonPath = orgPathTemp,
                        TranType = 4
                    };
                    var hasMapping = mappings.FirstOrDefault(m => m.AimJsonPath == mapping.AimJsonPath && m.TranType == 4) != null;
                    if (!hasMapping)
                    {
                        mappings.Insert(index, mapping);
                        tempMappings.Insert(index, mapping_Temp);
                    }

                    tempMappings.ForEach(mappingItemCur =>
                    {
                        mappingItemCur.OrgJsonPath = mappingItemCur.OrgJsonPath.Replace($"{orgPathTemp}.*", $"{ orgPathTemp}.a");
                        mappingItemCur.AimJsonPath = mappingItemCur.AimJsonPath.Replace($"{aimPath}[*]", $"{aimPath}[0]");
                    });
                }
            }

        }

        /// <summary>
        /// 压缩JSON
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        /// <param name=""></param>
        /// <param name=""></param>
        /// <param name=""></param>
        /// <param name=""></param>
        private JToken CompressJson(JToken obj, string currentPath, List<string> paths)
        {
            if (obj == null || obj.Type != JTokenType.Object && obj.Type != JTokenType.Array)
            {
                return obj;
            }

            if (obj.Type == JTokenType.Array)
            {
                JArray objArray = obj as JArray;

                if (objArray != null)
                {
                    var childList = objArray.Children();
                    List<JToken> temJsonNodeList = new List<JToken>();
                    for (int i = 0; i < childList.Count(); i++)
                    {
                        temJsonNodeList.Add(this.CompressJson(childList.ElementAt(i), $"{ currentPath}[{ i}]", paths));
                    }
                    objArray.RemoveAll();
                    temJsonNodeList.ForEach(item => {
                        objArray.Add(item);
                    });
                   
                }
                return objArray;
            }



            var objObject = obj as JObject;
            if (objObject != null)
            {
                Dictionary<string,JToken> temJsonNodeList = new Dictionary<string, JToken>();
                foreach (var item in objObject.Children())
                {
                    if (item is JProperty)
                    {
                        var newPath = !string.IsNullOrEmpty(currentPath) ? $"{ currentPath}.{((JProperty)item).Name}" : ((JProperty)item).Name;

                        if (paths.FirstOrDefault(p => p.Contains(newPath)) != null)
                        {
                            temJsonNodeList.Add( ((JProperty)item).Name, this.CompressJson(((JProperty)item).Value, newPath, paths));
                        }
                    }
                }
                objObject.RemoveAll();

                foreach (var key in temJsonNodeList.Keys)
                {
                    objObject.Add(key, temJsonNodeList[key]);
                }

            }

            return objObject;
        }


        #endregion


        #region 成员公共方法

        /// <summary>
        /// 获取Json的属性
        /// </summary>
        /// <returns></returns>
        public List<JsonProperty> GetJsonProperties(string jsonStr)
        {

            List<JsonProperty> keyList = new List<JsonProperty>();
            JToken jvalue = JValue.Parse(jsonStr);
            LoadJsonProperties(jvalue, keyList);

            //keyList.ForEach(key =>
            //{
            //    Console.WriteLine($@"Name:{key.Name}  Value:{key.Value}   Path:{key.Path}    Type:{key.Type} ");
            //});
            return keyList?.Distinct().ToList();
        }

        /// <summary>
        /// Json转换
        /// </summary>
        /// <returns></returns>
        public string TranJson()
        {

            JToken jOrg = JValue.Parse(this.OrgTemplate);
            JToken jAim = JValue.Parse(this.AimTemplate);

            Console.WriteLine("******************转换前  JAIM **********************");
            Console.WriteLine(JsonConvert.SerializeObject(jAim));
            Console.WriteLine("******************转换前  Mapping **********************");
            foreach (var item in this.JsonMappings)
            {
                Console.WriteLine(JsonConvert.SerializeObject(item));
            }

            BuildJsonMapping_Init(this.JsonMappings);
            Console.WriteLine("******************初始化后的  Mapping **********************");
            foreach (var item in this.JsonMappings)
            {
                Console.WriteLine(JsonConvert.SerializeObject(item));
            }

            BuildJsonMapping(jOrg, jAim, jAim, this.JsonMappings);

            Console.WriteLine("******************重新构造后的  Mapping **********************");
            foreach (var item in this.JsonMappings)
            {
                Console.WriteLine(JsonConvert.SerializeObject(item));
            }
            Console.WriteLine("******************重新构造后的  JAIM **********************");
            Console.WriteLine(JsonConvert.SerializeObject(jAim));

            TranJson_Inner(jOrg, jAim, jAim, this.JsonMappings);

            Console.WriteLine("******************转换后  JAIM **********************");
            Console.WriteLine(JsonConvert.SerializeObject(jAim));

            Console.WriteLine("******************转换后  Mapping **********************");
            foreach (var item in this.JsonMappings)
            {
                Console.WriteLine(JsonConvert.SerializeObject(item));
            }

            return jAim.ToString();
        }

        /// <summary>
        /// 检查JsonMapping信息
        /// </summary>
        /// <returns></returns>
        public List<dynamic> CheckJsonMapping()
        {
            List<dynamic> checkResults = new List<dynamic>();


            JToken jOrg = JValue.Parse(this.OrgTemplate);
            JToken jAim = JValue.Parse(this.AimTemplate);


            this.jsonMappings.ForEach(jsonMapping =>
            {

                dynamic checkResult = new ExpandoObject();
                checkResult.Mapping = null;
                checkResult.AimMsg = "";
                checkResult.OrgMsg = "";

                var resultAimMsg = "";
                var resultOrgMsg = "";

                /***************************验证路径有效性*************************** */

                var result = Regex.Match(jsonMapping.OrgJsonPath, this.Json_Path_Regex).Success;
                if (!result)
                {
                    resultOrgMsg = $"{ resultOrgMsg}【{jsonMapping.OrgJsonPath}】Json路径验证失败！";
                }
                result = Regex.Match(jsonMapping.AimJsonPath, this.Json_Path_Regex).Success;
                if (!result)
                {
                    resultAimMsg = $"{ resultAimMsg}【{jsonMapping.AimJsonPath}】Json路径验证失败！";
                }




                /***************************验证路径是否定位到属性*************************** */

                //验证源路径
                String orgJsonPath = jsonMapping.OrgJsonPath.Replace("[*]", "[0]");
                while (orgJsonPath.Contains(".*"))
                {
                    var tempOrgJsonPath = orgJsonPath.Substring(0, orgJsonPath.IndexOf(".*"));
                    if (!string.IsNullOrEmpty(tempOrgJsonPath))
                    {
                        var jsonMemberTemp = jOrg.SelectToken(tempOrgJsonPath).Ancestors().FirstOrDefault(c => c.Type == JTokenType.Property);
                        if (jsonMemberTemp == null)
                        {
                            resultOrgMsg = $"{ resultOrgMsg}【{jsonMapping.OrgJsonPath }】Json路径无法定位到json属性！";
                        }
                        if (jsonMemberTemp.Children().Count() <= 0)
                        {
                            resultOrgMsg = $"{ resultOrgMsg}【{jsonMapping.OrgJsonPath }】Json路径没有子属性！";
                        }
                        orgJsonPath = orgJsonPath.Replace(tempOrgJsonPath + ".*", jsonMemberTemp.Children().FirstOrDefault(c => c.Type == JTokenType.Property)?.Path);
                    }

                }
                JToken jsonMember = null;
                try
                {
                    jsonMember = jOrg.SelectToken(orgJsonPath)?.Ancestors()?.FirstOrDefault(c => c.Type == JTokenType.Property);
                }
                catch (Exception)
                {
                }
                if (jsonMember == null)
                {
                    resultOrgMsg = $"{ resultOrgMsg}【{jsonMapping.OrgJsonPath }】Json路径无法定位到json属性！";
                }

                //验证目标路径
                String aimJsonPath = jsonMapping.AimJsonPath.Replace("[*]", "[0]");
                while (aimJsonPath.Contains(".*"))
                {
                    var tempAimJsonPath = aimJsonPath.Substring(0, aimJsonPath.IndexOf(".*"));
                    if (!string.IsNullOrEmpty(tempAimJsonPath))
                    {
                        var jsonMemberAimTemp = jAim.SelectToken(tempAimJsonPath).Ancestors().FirstOrDefault(c => c.Type == JTokenType.Property);

                        if (jsonMemberAimTemp == null)
                        {
                            resultAimMsg = $"{ resultAimMsg}【{ jsonMapping.AimJsonPath}】Json路径无法定位到json属性！";
                        }
                        if (jsonMemberAimTemp.Children().Count() <= 0)
                        {
                            resultAimMsg = $"{ resultAimMsg}【{ jsonMapping.AimJsonPath}】Json路径没有子属性！";
                        }
                        aimJsonPath = aimJsonPath.Replace(tempAimJsonPath + ".*", jsonMemberAimTemp.Children().FirstOrDefault(c => c.Type == JTokenType.Property)?.Path);
                    }

                };
                var jsonMemberAim = jAim.SelectToken(aimJsonPath)?.Ancestors().FirstOrDefault(c => c.Type == JTokenType.Property);
                if (jsonMemberAim == null)
                {
                    resultAimMsg = $"{ resultAimMsg}【{ jsonMapping.AimJsonPath}】Json路径无法定位到json属性！";
                }

                checkResult.OrgMsg = resultOrgMsg;
                checkResult.AimMsg = resultAimMsg;

                checkResults.Add(checkResult);

            });
            return checkResults;

        }

        /// <summary>
        /// 获取压缩后的源Json信息
        /// </summary>
        public string GetSimpleOrgJson()
        {

            JToken tempOrgTemplate = JValue.Parse(this.OrgTemplate);
            JToken tempAimTemplate = JValue.Parse(this.AimTemplate);

            var tempMappings = JsonConvert.DeserializeObject<List<JsonMapping>>(JsonConvert.SerializeObject(this.jsonMappings));



            //进行初步构造位数组转*的情况，添加父级节点的映射（TranType=4）
            this.BuildJsonMapping_Init(tempMappings);

            this.BuildJsonMapping(tempOrgTemplate, tempAimTemplate, tempAimTemplate, tempMappings);

            var tempOrgMappings = tempMappings.Select(item =>
            {
                return item.OrgJsonPath;
            }).ToList();



            var compressJson = this.CompressJson(tempOrgTemplate.SelectToken("root"), "root", tempOrgMappings);
            return compressJson.ToString();

        }

        #endregion

    }

    /// <summary>
    /// Json中的属性信息
    /// </summary>
    public class JsonProperty
    {

        public string Path { get; set; }
        public JTokenType Type { get; set; }
        public string Name { get; set; }
        public JToken Value { get; set; }

        public string Desc { get; set; }

    }

    /// <summary>
    /// Json映射
    /// </summary>
    public class JsonMapping
    {

        /// <summary>
        /// 要转换的目标Json路径
        /// </summary>
        public string AimJsonPath { get; set; }

        /// <summary>       
        ///要转换的源Json路径
        /// </summary>
        public string OrgJsonPath { get; set; }


        /// 转换类型
        /// 1：源Key->目标Key
        /// 2：源Key->目标Value
        /// 3：源Value->目标Key
        /// 4：源Value->目标Value
        public int TranType { get; set; }

        public JsonMapping()
        {

        }
        public JsonMapping(string aimJsonPath, string orgJsonPath, int tranType)
        {
            this.OrgJsonPath = orgJsonPath;
            this.AimJsonPath = aimJsonPath;
            this.TranType = tranType;
        }


    }
}
