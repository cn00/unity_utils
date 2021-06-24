#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine.Serialization;
// using Debug = UnityEngine.Debug;
using UnityEditor;
using Object = System.Object;

static class RectExtension
{
    public static Rect Split(this Rect rect, int index, int count)
    {
        int r = (int) rect.width % count; // Remainder used to compensate width and position.
        int width = (int) (rect.width / count);
        rect.width = width + (index < r ? 1 : 0) + (index + 1 == count ? (rect.width - (int) rect.width) : 0f);
        if (index > 0)
        {
            rect.x += width * index + (r - (count - 1 - index));
        }

        return rect;
    }
}
/// <summary>
/// Assets/ABResources 下需要打包的资源目录配置
/// </summary>
public class InspectorUtil
{
    public static IEnumerator Execute(string exe, string prmt
        , DataReceivedEventHandler OutputDataReceived = null
        , Action end = null
        , int total = 0, string processingtag = "bash", string info = ""
        , string WorkingDirectory = "."
    )
    {
        bool redirectio = true;
        int Finished = 0;
        var Progress = 0f;
        var process = new System.Diagnostics.Process();
        var promatch = new Regex(processingtag);
        Action<string> OutStringAct = s =>
        {
            var match = promatch.Match(s);
            if (match.Groups["Total"] != null)
            {
                int.TryParse(match.Groups["Total"].Value, out total);
            }

            if (match.Groups["Finished"] != null)
            {
                int.TryParse(match.Groups["Finished"].Value, out Finished);
            }

            if (match.Groups["Progress"] != null)
            {
                float.TryParse(match.Groups["Progress"].Value, out Progress);
                if (Progress > 1) Progress /= 100;
            }
            // else
            if (Finished > 0 && total > 0)
            {
                Progress = Finished / total;
            }
        };
        DataReceivedEventHandler OutputAct = (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;
            OutStringAct(e.Data);
        };

        // UnityEngine.Debug.Log(exe + " " + prmt);
        ProcessStartInfo pi = new ProcessStartInfo(exe, prmt);
        // pi.WindowStyle = ProcessWindowStyle.Hidden;
        pi.WorkingDirectory = WorkingDirectory;
        // pi.RedirectStandardInput = redirectio;
        pi.RedirectStandardOutput = redirectio;
        pi.RedirectStandardError = redirectio;
        pi.UseShellExecute = !redirectio;
        pi.CreateNoWindow = !redirectio;
        pi.StandardErrorEncoding = System.Text.UTF8Encoding.UTF8;
        pi.StandardOutputEncoding = System.Text.UTF8Encoding.UTF8;

        if (false)
        {
            if (OutputDataReceived != null)
                process.OutputDataReceived += OutputDataReceived;
            process.OutputDataReceived += OutputAct;
            process.ErrorDataReceived += OutputAct;

            process.Exited += (sender, e) =>
            {
                // var log = process.StandardOutput.ReadToEnd();
                // UnityEngine.Debug.Log("cmdlog:" + log);

                UnityEngine.Debug.Log(exe + " " + prmt.Substring(0, Mathf.Min(prmt.Length, 15)) + " ... Exit");
                UnityEngine.Debug.Log("finished.");
            };

            if (redirectio)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            // process.WaitForExit(10000);
        }

        process.StartInfo = pi;
        // process.EnableRaisingEvents = true;
        process.Start();

        var fakepg = 0;
        while (!process.StandardOutput.EndOfStream)
        {
            string line = process.StandardOutput.ReadLine();
            OutStringAct(line);
            UnityEngine.Debug.Log(line);

            var pg = Progress / total;
            if (total < 2)
            {
                pg = (float)(Math.Atan(fakepg/300)/Math.PI*2f);
                ++fakepg;
            }
            EditorUtility.DisplayCancelableProgressBar(processingtag + " ...", info + ": " + pg + "/" + Progress + "/" + total, pg);
            yield return null;
        }

        if (end != null)
            end();

        UnityEngine.Debug.Log("finished: " + process.ExitCode);
        EditorUtility.ClearProgressBar();
        yield return null;
    }

    private static bool showidx = false;
    private static int PageLen = 30;
    static Dictionary<string, int> tmpCurrentPage = new Dictionary<string, int>();
    public static void ResetCurrentPage(string name) => tmpCurrentPage[name] = 0;

    public static string ListFilter;
    public static void DrawList(string name, List<object> ls, ref bool foldout, GUILayoutOption[] options = null,Func<string, bool> filter = null)
    {
        Func<string, bool> f = iname =>
        {
            if (!string.IsNullOrEmpty(ListFilter))
                return Regex.Match(iname, ListFilter).Length > 0;
            return true;
        };
        filter = filter ?? f;

        ls = ls.Where(i=>{
            var iname = i.GetType().GetField("name")?.GetValue(i)?.ToString() ?? "";
            return filter(iname);
        }).ToList();
        
        var size = ls.Count;
        EditorGUILayout.BeginHorizontal();
        {
            foldout = EditorGUILayout.Foldout(foldout, (foldout ? "-" : "+") + name, true);
            size = EditorGUILayout.DelayedIntField(size);
        }
        EditorGUILayout.EndHorizontal();
        // ++EditorGUI.indentLevel;

        if (size < ls.Count)
        {
            // ls.RemoveRange(size - 1, ls.Count - size);
            while (ls.Count > size)
            {
                ls.RemoveAt(ls.Count - 1);
            }
        }
        else if (size > ls.Count)
        {
            var it = ls.GetType().GetGenericArguments().Single();
            for (var i = ls.Count; i < size; ++i)
                ls.Add(Default(it));
        }

        if (foldout)
        {
            int currentPage = 0;
            tmpCurrentPage.TryGetValue(name, out currentPage);
            ListFilter = EditorGUILayout.DelayedTextField("Filter", ListFilter);
            showidx = EditorGUILayout.Toggle("showidx", showidx);
            EditorGUILayout.BeginHorizontal(); if(true)
            {
                PageLen = EditorGUILayout.DelayedIntField("PageLen", PageLen);
                var pc = Math.Max(1, (ls.Count + PageLen - 1) / PageLen);

                currentPage = currentPage % pc;
                var rect = EditorGUILayout.GetControlRect();
                var sc = 2;
                var idx = -1;
                if (GUI.Button(rect.Split(++idx, sc), string.Format("--({0}/{1})", 1+currentPage, pc)))
                {
                    --currentPage;
                    currentPage = currentPage < 0 ? 0 : currentPage;
                }
                if (GUI.Button(rect.Split(++idx, sc), "++"))
                {
                    ++currentPage;
                }
                currentPage = currentPage % pc;
                tmpCurrentPage[name] = currentPage;
            }
            EditorGUILayout.EndHorizontal();

            for (var i = currentPage*PageLen; i < Math.Min((1+currentPage)*PageLen, ls.Count);)
            {
                var iobj = ls[i];
                var iname = iobj.GetType().GetField("name")?.GetValue(iobj)?.ToString() ?? "";
                
                if (showidx)
                    DrawObj(i.ToString(), ref iobj);
                else
                {
                    DrawObj(iname, ref iobj);
                }

                ls[i] = iobj;
                ++i;
            } // foreach
        }

        // --EditorGUI.indentLevel;
    }

    protected static object Default(Type t)
    {
        if (t.IsValueType)
            return default(int);
        else if (t == typeof(string))
        {
            return "";
        }
        else
        {
            return Activator.CreateInstance(t);
        }
    }

    public static void DrawObj(string name, ref object obj, Action begin = null, Action end = null)
    {
        if (begin != null) begin(); // once only
        var v = obj;
        if (v is bool)
        {
            obj = EditorGUILayout.Toggle(name, (bool) v);
        }
        else if (v is Enum)
        {
            if (name.ToLower().EndsWith("s"))
            {
                obj = EditorGUILayout.EnumMaskField(name, (Enum) v);
            }
            else
            {
                obj = EditorGUILayout.EnumPopup(name, (Enum) v);
            }
        }
        else if (v is int)
        {
            obj = EditorGUILayout.IntField(name, (int) v);
        }
        else if (v is uint)
        {
            var vv = Convert.ToInt64(v);
            obj = EditorGUILayout.LongField(name, vv);
        }
        else if (v is long)
        {
            if (name.ToLower().Contains("time"))
            {
                EditorGUILayout.LabelField(name, DateTime.FromFileTime((long) v).ToString());
            }
            else
            {
                obj = EditorGUILayout.LongField(name, (long) v);
            }
        }
        else if (v is double || v is float)
        {
            obj = EditorGUILayout.FloatField(name, (float) v);
        }
        else if (v is string)
        {
            if (name.ToLower().EndsWith("s"))
            {
                EditorGUILayout.LabelField(name);
                obj = EditorGUILayout.TextArea((string) v)
                    .Replace("\r", "")
                    .Replace("\n\n", "\n")
                    .TrimStart(' ', '\n', '\t')
                    .TrimEnd(' ', '\n', '\t');
            }
            else
            {
                // EditorGUILayout.BeginHorizontal();
                // EditorGUILayout.LabelField(name, new GUIStyle() {fixedWidth = 0.2f});
                // obj = EditorGUILayout.TextField( (string) v);
                // EditorGUILayout.EndHorizontal();

                obj = EditorGUILayout.DelayedTextField(name, (string) v);
            }
        }
        else
        {
            ++EditorGUI.indentLevel;
            using (var verticalScope2 = new EditorGUILayout.VerticalScope("box"))
            {
                DrawComObj(name, obj);
            }

            --EditorGUI.indentLevel;
        }

        if (end != null) end();
    }

    static Dictionary<string, bool> tmpFoldout = new Dictionary<string, bool>();

    public static void DrawComObj(string name, object obj, Action begin = null, Action end = null,
        Color color = default(Color), Func<MemberInfo, bool> filter = null)
    {
        Func<MemberInfo, bool> f = info => true;
        filter = filter ?? f;
        
        if (obj == null) return;
        if (obj is IList)
        {
            var foldout = true;
            tmpFoldout.TryGetValue(name, out foldout);
            DrawList(name, obj as List<object>, ref foldout);
            tmpFoldout[name] = foldout;
        }
        else
        {
            var type = obj.GetType();
            var fields = type.GetFields(
                BindingFlags.Default
                // | BindingFlags.DeclaredOnly // no inherited
                | BindingFlags.Instance
                // | BindingFlags.Public
            ).Where(i=> filter == null ? true : filter(i)).ToList();
            //必须指定 BindingFlags.Instance 或 BindingFlags.Static。
            var prs = type.GetProperties(
                BindingFlags.Default
                | BindingFlags.Instance
                | BindingFlags.Public
                // | BindingFlags.SetField
            ).Where(i=> filter == null ? true : filter(i)).ToList();

            if (string.IsNullOrEmpty(name))
            {
                var fnobj = fields.Find(ifobj => ifobj.Name.ToLower() == "name");
                if( fnobj != null)
                    name = (string) fnobj.GetValue(obj);
                else
                {
                    var pnobj = prs.Find(i => i.Name.ToLower() == "name");
                    if( pnobj != null)
                        name = (string) pnobj.GetValue(obj);
                    else
                        name = type.Name;
                }
            }

            bool tmpfoldobj = false;
            var ffobj = fields.Find(ifobj => ifobj.Name == "Foldout");
            if (ffobj != null)
                tmpfoldobj = (bool) ffobj.GetValue(obj);
            else
            {
                if (!tmpFoldout.TryGetValue(name, out tmpfoldobj))
                    tmpfoldobj = (tmpFoldout[name] = false);
            }

            if (color == default(Color))
                color = Color.black;
            var style = new GUIStyle(EditorStyles.foldout);
            style.normal.textColor = color;
            tmpfoldobj = EditorGUILayout.Foldout(tmpfoldobj, name, true, style);
            if (ffobj != null)
                ffobj.SetValue(obj, tmpfoldobj);
            else
                tmpFoldout[name] = tmpfoldobj;
            if (!tmpfoldobj)
            {
                return;
            }

            // ++EditorGUI.indentLevel;
            var actionfild = type.GetMethods(
                BindingFlags.Default
                | BindingFlags.DeclaredOnly // no inherited
                | BindingFlags.Instance
                // | BindingFlags.Public
                // | BindingFlags.InvokeMethod
            ).ToList();
            // Methods
            if(true)
            {
                var btns = actionfild.Where(i => i.Name.EndsWith("Btn") || i.Name.StartsWith("Btn")).ToList();
                var NumPerRow = 3;
                for (var rowi = 0;
                    btns.Count > 0 && rowi < (btns.Count + NumPerRow * 0.5) / NumPerRow;
                    ++rowi)
                {
                    EditorGUILayout.Space();
                    var rect = EditorGUILayout.GetControlRect();
                    for (var i = 0; i < NumPerRow && (NumPerRow * rowi + i) < btns.Count; ++i)
                    {
                        var fi = btns[NumPerRow * rowi + i];
                        var v = fi.GetParameters();
                        if (GUI.Button(rect.Split(i, NumPerRow), fi.Name.Replace("Btn", "")))
                        {
                            fi.Invoke(obj, v);
                            return;
                        }
                    }
                }

                var headf = actionfild.Find(i => i.Name == "HeadDraw");
                if (headf != null)
                {
                    var v = headf.GetParameters();
                    headf.Invoke(obj, v);
                }
            }

            if (begin != null) begin();

            fields.Sort((i, j) =>
            {
                // if ((i.GetValue(obj) is IList) && !(j.GetValue(obj) is IList))
                // {
                //     return -1;
                // }
                //
                // if ((j.GetValue(obj) is IList) && !(i.GetValue(obj) is IList))
                // {
                //     return 1;
                // }
                //
                // if ((i.GetValue(obj) is ValueType) && !(j.GetValue(obj) is ValueType))
                // {
                //     return -1;
                // }
                //
                // if ((j.GetValue(obj) is ValueType) && !(i.GetValue(obj) is ValueType))
                // {
                //     return 1;
                // }
                //
                // if ((i.GetValue(obj) is Enum) && !(j.GetValue(obj) is Enum))
                // {
                //     return -1;
                // }
                // if ((j.GetValue(obj) is Enum) && !(i.GetValue(obj) is Enum))
                // {
                //     return 1;
                // }
                return i.Name.CompareTo(j.Name);
            });
            // ++EditorGUI.indentLevel;
            foreach (var i in fields)
            {
                if (i.Name.EndsWith("Foldout"))
                    continue;
                var v = i.GetValue(obj);
                // if ((v is bool)|| (v is Enum)|| (v is int)||(v is uint)||( v is long)|| (v is double )||( v is float)|| (v is string))
                if ((v is ValueType) || (v is string))
                {
                    DrawObj(i.Name, ref v);
                    i.SetValue(obj, v);
                }
                else if (v is IList)
                {
                    var il = v as List<object>;
                    var gk = name + "." + i.Name;
                    var foldout = true;
                    {
                        if (!tmpFoldout.TryGetValue(gk, out foldout))
                            foldout = (tmpFoldout[gk] = false);
                    }
                    ++EditorGUI.indentLevel;
                    DrawList(i.Name, il, ref foldout);
                    --EditorGUI.indentLevel;
                    {
                        tmpFoldout[gk] = foldout;
                    }
                }
                else if (v is string[])
                {
                    EditorGUILayout.LabelField(i.Name);
                    ++EditorGUI.indentLevel;
                    var l = v as string[];
                    for (var idx = 0; idx < l.Length; ++idx)
                    {
                        l[idx] = EditorGUILayout.TextField(l[idx]);
                    }

                    --EditorGUI.indentLevel;
                }
                else if (v is Action)
                {
                    // skip
                }
                else if (v is object)
                {
                    ++EditorGUI.indentLevel;
                    using (var verticalScope2 = new EditorGUILayout.VerticalScope("box"))
                    {
                        DrawComObj(i.Name, v);
                    }

                    --EditorGUI.indentLevel;
                }
            }
            // --EditorGUI.indentLevel;

            //properties
            if(true)
            {
                foreach (var i in prs)
                {
                    if (i.Name.EndsWith("Foldout"))
                        continue;
                    var v = i.GetValue(obj, null);
                    // if ((v is bool)|| (v is Enum)|| (v is int)||(v is uint)||( v is long)|| (v is double )||( v is float)|| (v is string))
                    if ((v is ValueType) || (v is string))
                    {
                        DrawObj(i.Name, ref v);
                        // if (i.CanWrite)
                        //     i.SetValue(obj, v, null);
                    }
                    else if (v is IList)
                    {
                        var il = v as List<object>;
                        var gk = name + "." + i.Name;
                        var foldout = true;
                        {
                            if (!tmpFoldout.TryGetValue(gk, out foldout))
                                foldout = (tmpFoldout[gk] = false);
                        }
                        ++EditorGUI.indentLevel;
                        DrawList(i.Name, il, ref foldout);
                        --EditorGUI.indentLevel;
                        {
                            tmpFoldout[gk] = foldout;
                        }
                    }
                    else if (v is string[])
                    {
                        EditorGUILayout.LabelField(i.Name);
                        ++EditorGUI.indentLevel;
                        var l = v as string[];
                        for (var idx = 0; idx < l.Length; ++idx)
                        {
                            l[idx] = EditorGUILayout.TextField(l[idx]);
                        }

                        --EditorGUI.indentLevel;
                    }
                    else if (v is Action)
                    {
                        // skip
                    }
                    else if (v is object)
                    {
                        ++EditorGUI.indentLevel;
                        using (var verticalScope2 = new EditorGUILayout.VerticalScope("box"))
                        {
                            DrawComObj(name, v);
                        }

                        --EditorGUI.indentLevel;
                    }
                }
            }

            //GetNestedTypes
            if(false)
            {
                var nesteds = type.GetNestedTypes();
                foreach (var i in nesteds)
                {
                    ++EditorGUI.indentLevel;
                    if (!i.IsAbstract)
                        using (var verticalScope2 = new EditorGUILayout.VerticalScope("box"))
                        {
                            DrawComObj(name, obj);
                        }

                    --EditorGUI.indentLevel;
                }
            }


            var tailf = actionfild.Find(i => i.Name == "TailDraw");
            if (tailf != null)
            {
                var v = tailf.GetParameters();
                tailf.Invoke(obj, v);
            }

            if (end != null) end();
        }
    }

    public static bool IsJenkinsBuild()
    {
        string jenkins = Environment.GetEnvironmentVariable("JENKINS_URL");
        return !string.IsNullOrEmpty(jenkins);
    }

    public static string MD5(string source)
    {
        byte[] sor = Encoding.UTF8.GetBytes(source);
        var md5 = System.Security.Cryptography.MD5.Create();
        byte[] result = md5.ComputeHash(sor);
        var strbul0 = System.Text.Encoding.Default.GetString(result);
        var strbul = "";
        for (int i = 0; i < result.Length; i++)
        {
            //"x2"结果为32位,"x3"结果为48位,"x4"结果为64位
            strbul += result[i].ToString("x2");
        }

        return strbul;
    }

        /// <summary>
        /// 目录拷贝
        /// </summary>
        /// <param name="srcPath"></param>
        /// <param name="tarPath"></param>
        private static void CopyFolder(string srcPath, string tarPath)
        {
            if (!Directory.Exists(srcPath))
            {
                return;
            }

            if (!Directory.Exists(tarPath))
            {
                Directory.CreateDirectory(tarPath);
            }

            CopyFile(srcPath, tarPath);
            string[] directionName = Directory.GetDirectories(srcPath);
            foreach (string dirPath in directionName)
            {
                string directionPathTemp = tarPath + "\\" + dirPath.Substring(srcPath.Length + 1);
                CopyFolder(dirPath, directionPathTemp);
            }
        }

        /// <summary>
        /// 文件拷贝
        /// </summary>
        /// <param name="srcPath"></param>
        /// <param name="tarPath"></param>
        private static void CopyFile(string srcPath, string tarPath)
        {
            string[] filesList = Directory.GetFiles(srcPath);
            foreach (string f in filesList)
            {
                string fTarPath = tarPath + "\\" + f.Substring(srcPath.Length + 1);
                if (File.Exists(fTarPath))
                {
                    File.Copy(f, fTarPath, true);
                }
                else
                {
                    File.Copy(f, fTarPath);
                }
            }
        }

        public static byte[] EncodeFile(string inPath, string outPath, string password)
        {
            var data = File.ReadAllBytes(inPath);
            //TODO: data = Security.XXTEA.Encrypt(data, password);
            File.WriteAllBytes(outPath, data);
            return data;
        }


        public static void AddMacroForScriptInDir(string dir, string macro)
        {
            if (!macro.StartsWith("#if"))
            {
                macro = "#if " + macro;
            }

            var css = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            foreach (var f in css)
            {
                var s = File.ReadAllText(f);
                if (!s.Contains(macro))
                {
                    s = macro + "\n" + s + "\n#endif //" + macro;
                }

                File.WriteAllText(f, s);
            }
        }

        public static void RemoveMacroForScriptInDir(string dir, string macro)
        {
            if (macro.StartsWith("#if"))
            {
                macro = "#if " + macro;
            }

            var css = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            foreach (var f in css)
            {
                var s = File.ReadAllText(f);
                if (!s.Contains(macro))
                {
                    s = s.Replace(macro + "\n", "")
                        .Replace("\n#endif //" + macro, "");
                }

                File.WriteAllText(f, s);
            }
        }
}
#endif // UNITY_EDITOR
