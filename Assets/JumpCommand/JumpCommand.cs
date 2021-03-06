﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using System.Reflection;
using Object = System.Object;
using Debug = UnityEngine.Debug;

static public class JumpCommand {
    static private  Dictionary<string, JumpCommandObject> mCmdLst = new Dictionary<string, JumpCommandObject>();
    static public   Object       Callee {get;set;}
    static private  List<string> History = new List<string>();
    static private  int  MaxHistoryNum = 100; 
    static private  int FirstHistoryIndex = -1;
    static JumpCommand() {}

    static public void Awake() {
        RegisterAllCommandObjects();
    }

    static private void RegisterAllCommandObjects() {
#if UNITY_EDITOR
        mCmdLst.Clear();
        var csharpDLL = Assembly.GetExecutingAssembly();  // unity csharp.dll assembly
        foreach( var t in csharpDLL.GetTypes() ) {        
            foreach(var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {  
                foreach(var a in m.GetCustomAttributes(true)) {
                    if(a.GetType() == typeof(JumpCommandRegister) ) {
                        string commandName = (a as JumpCommandRegister).mCommand;
                        string help        = (a as JumpCommandRegister).mHelp;
                        if(mCmdLst.ContainsKey(commandName)) {
                            Debug.LogError(string.Format("Can not register function {0} as command \"{1}\", \"{1}\" is already used by {2}.{3}", 
                                m.Name, commandName, mCmdLst[commandName].Type.Name, mCmdLst[commandName].Method.Name));
                        }
                        else {
                            mCmdLst.Add(commandName, new JumpCommandObject(commandName, m, help));
                        }
                    }
                }
            }
        }
#endif
    }

    static public bool Constains(string commandName) {
        return mCmdLst.ContainsKey(commandName);
    }

    // retrieve i-th history command, 0 is newest one.
    static public string GetHistory(int i) {
        if(History.Count == 0) return "";
        else return History[(FirstHistoryIndex + MaxHistoryNum - i) % MaxHistoryNum];
    }

    static private void AddHistory(string command) {
        FirstHistoryIndex = (FirstHistoryIndex + 1)%MaxHistoryNum;
        if(History.Count == MaxHistoryNum) {
            History[FirstHistoryIndex] = command;
        }
        else {
            History.Add(command);
        }
    }

    static public int HistoryNum {
        get {return History.Count;}
    }

    static public void CleanHistory() {
        History.Clear();
        FirstHistoryIndex = -1;
    }

    static public string GetAutoCompletionCommand(string Cmd, string FullCmd = "")
    {
        List<string> CmdArray = new List<string>();

        // Go through and get all commands start with input command
        foreach(string key in mCmdLst.Keys)
        {
            if (key.ToUpper().StartsWith(Cmd.ToUpper()))
            {
                CmdArray.Add(key);
            }
        }

        // Choose first matched command
        if (CmdArray.Count != 0)
        {
            CmdArray.Sort();

            int index = CmdArray.IndexOf(FullCmd);

            if (index != -1)
            {
                index++;
                index %= CmdArray.Count;
                return CmdArray[index];
            }
            else
            {
                return CmdArray[0];
            }
        }

        return null;
    }

    static public void Execute(string command) {
#if UNITY_EDITOR
        AddHistory(command);
        string[] argv = ParseArguments(command);
        if (argv.Length == 0) {
            Debug.LogError("Command is empty");
            return; 
        }
        if (mCmdLst.ContainsKey(argv[0])) {
            JumpCommandObject cmd = mCmdLst[argv[0]];
            string[] paramStr = new string[argv.Length - 1];
            Array.Copy(argv,1,paramStr,0,argv.Length-1);
            if(Application.isEditor && !mCmdLst[argv[0]].Method.IsStatic) {  // the callee will be the current select game object
                if(Selection.activeGameObject != null) {
                    var component = Selection.activeGameObject.GetComponent(mCmdLst[argv[0]].Type);
                    cmd.Call(paramStr, component);
                }
                else {
                    cmd.Call(paramStr, Callee);
                }
            }
            else {
                cmd.Call(paramStr, Callee);
            }
        }
        else {
            Debug.LogError(string.Format("Can not find command \"{0}\"",argv[0]));
        }
#endif
    }

    static private string[] ParseArguments(string commandLine) {
        char[] parmChars = commandLine.ToCharArray();
        bool inQuote = false;
        for (int index = 0; index < parmChars.Length; index++) {
            if (parmChars[index] == '"')
                inQuote = !inQuote;
            if (!inQuote && Char.IsWhiteSpace(parmChars[index]))
                parmChars[index] = '\n';
        }

        // remove double quote from begin and end
        string[] result = new string(parmChars).Split(new char[]{'\n'}, StringSplitOptions.RemoveEmptyEntries);
        for(int i = 0; i< result.Length; i++) {
            result[i] = result[i].Trim('"');
        }
        return result;            
    }

    [JumpCommandRegister("ls","list all the command")]
    static private void OutputAllCommnd() {
        foreach(var c in mCmdLst.Values) {
            string paramInfo = "(";
            foreach(var p in c.Method.GetParameters()) {
                paramInfo += p.ParameterType.Name + ", ";
            }
            paramInfo += ")";
            Debug.Log(string.Format("{0,-10} {1}: \"{2}\"", c.Command, paramInfo, c.Help));
        }        
    }
}

