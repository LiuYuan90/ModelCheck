using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3D模型检测脚本
/// </summary>
public class CheckModel : Editor
{
    #region 字符串
    /// <summary>
    /// 场景根节点名称
    /// </summary>
    public static string Env_Str = "Env";

    /// <summary>
    /// 检测结果输出的文件名
    /// </summary>
    public static string CheckResultFileName_Str = "/CheckResult.txt";

    /// <summary>
    /// 检测开始提示语
    /// </summary>
    public static string CkeckStart_Str = "----------------------------------检测开始----------------------------------";

    /// <summary>
    /// 检测结束提示语
    /// </summary>
    public static string CkeckDone_Str = "----------------------------------检测完成----------------------------------";
    #endregion

    /// <summary>
    /// 根节点
    /// </summary>
    static GameObject Env;

    /// <summary>
    /// 检测出的信息
    /// </summary>
    static StringBuilder _outPutInfo = new StringBuilder();

    /// <summary>
    /// 清除编辑器中的Debug信息。只能这么写，具体原因没弄懂，放进方法里就报空。
    /// 想研究可以看看这篇：https://answers.unity.com/questions/578393/clear-console-through-code-in-development-build.html
    /// </summary>
    static MethodInfo _clearConsoleMethod;

    /// <summary>
    /// 清除编辑器中的Debug信息
    /// </summary>
    static MethodInfo ClearConsoleMethod
    {
        get
        {
            if (_clearConsoleMethod == null)
            {
                Assembly assembly = Assembly.GetAssembly(typeof(SceneView));
                Type logEntries = assembly.GetType("UnityEditor.LogEntries");
                _clearConsoleMethod = logEntries.GetMethod("Clear");
            }
            return _clearConsoleMethod;
        }
    }

    #region 检测方法

    /// <summary>
    /// 开始检测的方法
    /// </summary>
    [MenuItem("EastSim/Check3DModel")]
    static void StartCheck()
    {
        //清除编辑器中的Debug信息。
        ClearConsoleMethod.Invoke(new object(), null);

        Env = Selection.activeGameObject;
        if (Env == null)
        {
            Debug.LogError("检测前要先选中场景根节点。");
            return;
        }
        if (Env.transform.childCount <= 5)
            AddOutPutInfo(string.Format("选中的对象名称为{0}，但是它的子物体数量少于5个，理论上是不正常的，是否选对了场景根节点？", Env.name));

        //准备工作
        PreparatoryWork();

        //检测重名
        CheckRepetitionName();

        //碰撞片检测
        CheckCollider();

        int secondGradeCount = Env.transform.childCount;
        for (int i = 0; i < secondGradeCount; i++)
        {
            GameObject tempParent = Env.transform.GetChild(i).gameObject;
            string secondGradeChildName = tempParent.name;

            if (tempParent.GetComponent<Component>() != null)
                OutPutInfoHavePath(string.Format("{0}上挂有组件，{1}下应该只有各个分组对象，分组对象都应该是虚拟物（空点）", secondGradeChildName, Env.name), tempParent);

            switch (secondGradeChildName)
            {
                case "T":
                case "D":
                case "P":
                case "E":
                case "A":
                case "F":
                case "B":
                case "R":
                case "S":
                case "C":
                case "K":
                case "M":
                case "O":

                    break;
                case "SIS":
                    CheckSCRIPT(tempParent);
                    break;
                case "CollectionPoint":
                    
                    break;
                case "OTR":

                    break;
                case "TKD":
                    CheckTKD(tempParent);
                    break;
                case "PipeGroup":
                    CheckPipeGroup(tempParent);
                    break;
                case "FloorGroup":
                    CheckFloorGroup(tempParent);
                    break;
                case "InclinedladderColider":
                    CkeckInclinedladderColider(tempParent);
                    break;
                case "Hide_Models":

                    break;
                case "ValveGroup":
                    //阀门替代物目前包含两类
                    //1、旧版中的阀门，有names.esp文件保存阀门信息，这些替代物以“ValveGroup”开头。
                    //2、3D组去客户初审的时候，帮工程部同事添加的位置标记替代物，这些替代物的名称是该阀的位号。
                    //所以这个分类目前无法检测。
                    break;
                case "Collision":
                    CheckCollision(tempParent);
                    break;
                case "FireControllerGroup":

                    break;
                default:
                    //AddOutPutInfo(string.Format("{0}的子物体应该只包含已知的分组，“{1}”不是已知组的名称", GlobalString.Env_Str, secondGradeChildName));
                    break;
            }
        }

        OutputInfo();
    }

    /// <summary>
    /// 检测重名。
    /// 重名是整个场景的问题，所以单独做。
    /// 而且，如果放到每个分类节点中做的话，会影响递归方法。
    /// </summary>
    static void CheckRepetitionName()
    {
        List<Transform> transList = new List<Transform>();
        List<string> repetitionNameList = new List<string>();

        transList.AddRange(Env.transform.GetComponentsInChildren<Transform>());

        int tempCount = transList.Count;
        for (int i = 0; i < tempCount; i++)
        {
            GameObject tempGo = transList[i].gameObject;
            string childName = tempGo.name;

            //如果这个重复的名字已经检查出来了，就不需要再次检测了。
            if (repetitionNameList.Find(q => q.Equals(childName)) != null)
                continue;

            //是否存在重名
            List<Transform> tempList = transList.FindAll(p => p.name.Equals(childName));
            if (tempList.Count > 1)
            {
                repetitionNameList.Add(childName);
                int tempListCount = tempList.Count;
                StringBuilder tempSB = new StringBuilder();
                for (int j = 0; j < tempListCount; j++)
                    tempSB.Append("\n" + GetGameObjectPath(tempList[j].gameObject));
                AddOutPutInfo(string.Format("“{0}”存在重名。这些对象分别是：{1}", childName, tempSB.ToString()));
            }
        }
    }

    /// <summary>
    /// 碰撞器检测。
    /// 3D组应该只给碰撞片组添加Collider，其他对象都不应该添加。
    /// </summary>
    static void CheckCollider()
    {
        var colliderArr = Env.transform.GetComponentsInChildren<Collider>();
        if (colliderArr == null || colliderArr.Length == 0)
            return;
        int tempCount = colliderArr.Length;
        for (int i = 0; i < tempCount; i++)
        {
            GameObject tempGo = colliderArr[i].gameObject;
            string tempGoName = tempGo.name;
            OutPutInfoHavePath(string.Format("{0}带有碰撞器组件，场景中不应该存在带有碰撞器的对象", tempGoName), tempGo);
        }
    }

    /// <summary>
    /// 检测所有大设备下
    /// </summary>
    static void CheckObjBase()
    {

    }

    /// <summary>
    /// 检测动设备
    /// </summary>
    static void CheckSCRIPT(GameObject parent)
    {
        int tempChildOneCount = parent.transform.childCount;
        for (int i = 0; i < tempChildOneCount; i++)
        {
            GameObject tempChildTwo = parent.transform.GetChild(i).gameObject;
            string tempChildTwoName = tempChildTwo.name;
            //是否符合动设备命名规则
            if (tempChildTwoName.StartsWith("SCRIPT__"))
            {
                if (tempChildTwo.GetComponent<MeshFilter>() != null && tempChildTwo.GetComponent<Renderer>() != null)
                {
                    int tempChildCountThree = tempChildTwo.transform.childCount;
                    //如果动设备对象下还有子物体，递归判断
                    if (tempChildCountThree > 0)
                    {
                        for (int j = 0; j < tempChildCountThree; j++)
                            CheckSCRIPT(tempChildTwo.transform.GetChild(j).gameObject);
                    }
                }
                else
                {
                    OutPutInfoHavePath(string.Format("“{0}”对象是个虚拟物（空点），无法正常使用。", tempChildTwoName), tempChildTwo);
                }

            }
            else
            {
                OutPutInfoHavePath(string.Format("“{0}”不符合“动设备”命名规则。", tempChildTwoName), tempChildTwo);
            }
        }
    }

    /// <summary>
    /// 集合点
    /// </summary>
    /// <param name="parent"></param>
    static void CheckCollectionPoint(GameObject parent)
    {

    }

    /// <summary>
    /// 检测车辆停靠点
    /// </summary>
    /// <param name="parent"></param>
    static void CheckTKD(GameObject parent)
    {
        int childCountOne = parent.transform.childCount;
        for (int i = 0; i < childCountOne; i++)
        {
            GameObject tempGoTwo = parent.transform.GetChild(i).gameObject;
            string tempGoNameTwo = tempGoTwo.name;
            var tempNameArr = Regex.Split(tempGoNameTwo, "__", RegexOptions.IgnoreCase);
            if (tempNameArr == null || tempNameArr.Length != 3 || !tempNameArr[0].Equals("TKD"))
            {
                string tempCarName = tempNameArr[1];
                if (tempCarName.Equals("XFC") || tempCarName.Equals("JHC") || tempCarName.Equals("QFC"))
                {
                    if (tempGoTwo.GetComponent<Component>() == null)
                    {
                        if (tempGoTwo.transform.childCount > 0)
                        {
                            OutPutInfoHavePath(string.Format("停靠点不应该有子物体，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                        }
                    }
                    else
                    {
                        OutPutInfoHavePath(string.Format("停靠点应该只是个虚拟物（空点），本对象可能挂有其他组件，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                    }
                }
                else
                {
                    OutPutInfoHavePath(string.Format("车辆的信息不是已知的车辆缩写，第二个字符串应该是车辆缩写，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                }
            }
            else
            {
                OutPutInfoHavePath(string.Format("停靠点命名不符合要求，对象名为{0}", tempGoNameTwo), tempGoTwo);
            }
        }
    }

    /// <summary>
    /// 管线
    /// </summary>
    /// <param name="parent"></param>
    static void CheckPipeGroup(GameObject parent)
    {
        int childCountOne = parent.transform.childCount;
        for (int i = 0; i < childCountOne; i++)
        {
            GameObject tempGoTwo = parent.transform.GetChild(i).gameObject;
            string tempGoNameTwo = tempGoTwo.name;
            var tempNameArr = tempGoNameTwo.Split('_', '*');
            if (tempNameArr == null || tempNameArr.Length != 3 || !tempNameArr[0].ToLower().Equals("gx"))
            {
                string tempCarName = tempNameArr[1];
                float result = 0f;
                float.TryParse(tempCarName, out result);
                if (result == 0f)
                {
                    if (tempGoTwo.transform.childCount > 0)
                    {
                        OutPutInfoHavePath(string.Format("管线不应该有子物体，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                    }
                }
                else
                {
                    OutPutInfoHavePath(string.Format("管线的第二个字符串不是管线直径，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                }
            }
            else
            {
                OutPutInfoHavePath(string.Format("管线命名不符合要求，对象名为{0}", tempGoNameTwo), tempGoTwo);
            }
        }
    }

    /// <summary>
    /// 检测地面组
    /// </summary>
    /// <param name="parent"></param>
    static void CheckFloorGroup(GameObject parent)
    {
        int childCountOne = parent.transform.childCount;
        for (int i = 0; i < childCountOne; i++)
        {
            GameObject tempGoTwo = parent.transform.GetChild(i).gameObject;
            string tempGoNameTwo = tempGoTwo.name;
            if (tempGoNameTwo.ToLower().StartsWith("pingtai"))
            {
                if (tempGoTwo.transform.childCount > 0)
                {
                    OutPutInfoHavePath(string.Format("地面不应该有子物体，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                }
            }
            else
            {
                OutPutInfoHavePath(string.Format("地面命名不符合要求，对象名为{0}", tempGoNameTwo), tempGoTwo);
            }
        }
    }

    /// <summary>
    /// 斜梯
    /// </summary>
    /// <param name="parent"></param>
    static void CkeckInclinedladderColider(GameObject parent)
    {
        int childCountOne = parent.transform.childCount;
        for (int i = 0; i < childCountOne; i++)
        {
            GameObject tempGoTwo = parent.transform.GetChild(i).gameObject;
            string tempGoNameTwo = tempGoTwo.name;
            if (tempGoNameTwo.ToLower().StartsWith("inclinedladdercolider"))
            {
                if (tempGoTwo.transform.childCount > 0)
                {
                    OutPutInfoHavePath(string.Format("斜梯不应该有子物体，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                }
            }
            else
            {
                OutPutInfoHavePath(string.Format("斜梯命名不符合要求，对象名为{0}", tempGoNameTwo), tempGoTwo);
            }
        }
    }

    /// <summary>
    /// 需要半透明处理的模型，比如中控室墙壁
    /// </summary>
    /// <param name="parent"></param>
    static void CkeckHide_Models(GameObject parent)
    {

    }

    /// <summary>
    /// 阀门替代物
    /// </summary>
    /// <param name="parent"></param>
    static void CheckValveGroup(GameObject parent)
    {
        
    }

    /// <summary>
    /// 碰撞片组
    /// </summary>
    static void CheckCollision(GameObject parent)
    {
        int childCountOne = parent.transform.childCount;
        for (int i = 0; i < childCountOne; i++)
        {
            GameObject tempGoTwo = parent.transform.GetChild(i).gameObject;
            string tempGoNameTwo = tempGoTwo.name;
            if (tempGoNameTwo.ToLower().StartsWith("pzp"))
            {
                if (tempGoTwo.transform.childCount > 0)
                {
                    OutPutInfoHavePath(string.Format("碰撞片不应该有子物体，对象名为：{0}", tempGoNameTwo), tempGoTwo);
                }
            }
            else
            {
                OutPutInfoHavePath(string.Format("碰撞片命名不符合要求，对象名为{0}", tempGoNameTwo), tempGoTwo);
            }
        }
    }


    /// <summary>
    /// 消防炮组
    /// </summary>
    static void CheckFireControllerGroup(GameObject parent)
    {

    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 检测开始之前的准备工作
    /// </summary>
    static void PreparatoryWork()
    {
        //添加检测开始的信息
        AddOutPutInfo("检测时间为：" + DateTime.Now.ToString("yyyy:MM:dd,HH:mm:ss"));
        AddOutPutInfo(CkeckStart_Str);
    }

    /// <summary>
    /// 输出提示信息，自动添加对象路径
    /// </summary>
    /// <param name="strValue"></param>
    /// <param name="go"></param>
    static void OutPutInfoHavePath(string strValue, GameObject go)
    {
        AddOutPutInfo(string.Format("{0}  对象路径为：{1}", strValue, GetGameObjectPath(go)));
    }

    /// <summary>
    /// 打印出错误信息，并把信息添加进_outPutInfo
    /// </summary>
    /// <param name="value">错误信息</param>
    static void AddOutPutInfo(string value)
    {
        Debug.LogError(value);
        _outPutInfo.Append(value);
        _outPutInfo.Append("\n\n");
    }

    /// <summary>
    /// 将检测结果输出到文件
    /// </summary>
    static void OutputInfo()
    {
        AddOutPutInfo(CkeckDone_Str);

        FileStream fs = new FileStream(Application.dataPath + CheckResultFileName_Str, FileMode.Create);
        StreamWriter sw = new StreamWriter(fs);
        //开始写入
        sw.Write(_outPutInfo.ToString());
        //清空缓冲区
        sw.Flush();
        //关闭流
        sw.Close();
        fs.Close();
    }

    /// <summary>
    /// 获取对象在Hierarchy中的层级路径
    /// </summary>
    /// <param name="go">对象</param>
    /// <returns>路径</returns>
    static string GetGameObjectPath(GameObject go)
    {
        Stack<GameObject> gameOStack = new Stack<GameObject>();
        Transform tempParent = go.transform;
        do
        {
            Transform tempGo = tempParent;
            gameOStack.Push(tempGo.gameObject);
            tempParent = tempGo.parent;
        } while (tempParent != null);

        StringBuilder tempSB = new StringBuilder();
        int stackCount = gameOStack.Count;
        for (int i = 0; i < stackCount; i++)
            tempSB.Append(gameOStack.Pop().name + "/");
        return tempSB.Remove(tempSB.Length - 1, 1).ToString();
    }

    #endregion
}
