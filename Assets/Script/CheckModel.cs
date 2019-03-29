using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3D模型检测脚本
/// </summary>
public class CheckModel : Editor
{
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
        //准备工作
        PreparatoryWork();

        Env = GameObject.Find(GlobalString.Env_Str);
        if (Env == null)
        {
            AddOutPutInfo("没有找到场景根节点，名称应该为：" + GlobalString.Env_Str);
            OutputInfo();
            return;
        }

        //先检测重名
        CheckRepetitionName();

        int secondGradeCount = Env.transform.childCount;
        for (int i = 0; i < secondGradeCount; i++)
        {
            GameObject tempParent = Env.transform.GetChild(i).gameObject;
            string secondGradeChildName = tempParent.name;
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

                    break;
                case "PipeGroup":

                    break;
                case "FloorGroup":

                    break;
                case "inclinedladderColider":

                    break;
                case "ValveGroup":

                    break;
                case "Collision":

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
    /// 检测重名
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

    #endregion

    #region 工具方法

    /// <summary>
    /// 检测开始之前的准备工作
    /// </summary>
    static void PreparatoryWork()
    {
        //清除编辑器中的Debug信息。
        ClearConsoleMethod.Invoke(new object(), null);

        //添加检测开始的信息
        AddOutPutInfo("检测时间为：" + DateTime.Now.ToString("yyyy:MM:dd,HH:mm:ss"));
        AddOutPutInfo(GlobalString.CkeckStart_Str);
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
        AddOutPutInfo(GlobalString.CkeckDone_Str);

        FileStream fs = new FileStream(Application.dataPath + GlobalString.CheckResultFileName_Str, FileMode.Create);
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
