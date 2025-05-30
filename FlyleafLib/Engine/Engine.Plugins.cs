﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using FlyleafLib.Plugins;

namespace FlyleafLib;

public class PluginsEngine
{
    public Dictionary<string, PluginType>
                    Types       { get; private set; } = new Dictionary<string, PluginType>();

    public string   Folder      { get; private set; }

    private Type pluginBaseType = typeof(PluginBase);

    internal PluginsEngine()
    {
        Folder = string.IsNullOrEmpty(Engine.Config.PluginsPath) ? null : Utils.GetFolderPath(Engine.Config.PluginsPath);

        LoadAssemblies();
    }

    internal void LoadAssemblies()
    {
        // Load FlyleafLib's Embedded Plugins
        LoadPlugin(Assembly.GetExecutingAssembly());

        // Load External Plugins Folder
        if (Folder != null && Directory.Exists(Folder))
        {
            string[] dirs = Directory.GetDirectories(Folder);

            foreach(string dir in dirs)
                foreach(string file in Directory.GetFiles(dir, "*.dll"))
                    LoadPlugin(Assembly.LoadFrom(Path.GetFullPath(file)));
        }
        else
        {
            Engine.Log.Info($"[PluginHandler] No external plugins found");
        }
    }

    /// <summary>
    /// Manually load plugins
    /// </summary>
    /// <param name="assembly">The assembly to search for plugins</param>
    public void LoadPlugin(Assembly assembly)
    {
        try
        {
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (pluginBaseType.IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                {
                    // Force static constructors to execute (For early load, will be useful with c# 8.0 and static properties for interfaces eg. DefaultOptions)
                    // System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);

                    if (!Types.ContainsKey(type.Name))
                    {
                        Types.Add(type.Name, new PluginType() { Name = type.Name, Type = type, Version = assembly.GetName().Version});
                        Engine.Log.Info($"Plugin loaded ({type.Name} - {assembly.GetName().Version})");
                    }
                    else
                        Engine.Log.Info($"Plugin already exists ({type.Name} - {assembly.GetName().Version})");
                }
            }
        }
        catch (Exception e) { Engine.Log.Error($"[PluginHandler] [Error] Failed to load assembly ({e.Message} {Utils.GetRecInnerException(e)})"); }
    }
}
