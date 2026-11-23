// Façade du Control Mapper de SimHub, pour FMT companion (src/fmt/). Formes relevées le 23/11/2026 dans la vraie
// SimHub.Plugins.dll du PC de Nathan (réflexion) et dans la DLL de FMT companion 0.5 compilée avec le vrai SimHub : mêmes
// noms, mêmes types, champ ou propriété comme dans SimHub. Les corps lèvent une exception : rien ici ne s'exécute jamais.
using System;
using System.Collections.ObjectModel;
using SimHub.Plugins.OutputPlugins.ControlRemapper.Models;

namespace SimHub.Plugins
{
    public partial class PluginManager
    {
        public delegate bool InputTriggeredDelegate(string input);

        public event InputTriggeredDelegate InputPressed { add => throw Stub(); remove => throw Stub(); }
        public event InputTriggeredDelegate InputReleased { add => throw Stub(); remove => throw Stub(); }

        public string GetCommonStoragePath(params string[] path) => throw Stub();

        public T GetPlugin<T>() where T : IPlugin => throw Stub();

        public void ShowPluginUI<T>() => throw Stub();
    }
}

namespace SimHub.Plugins.OutputPlugins.ControlRemapper
{
    public class ControlMapperPlugin : IPlugin
    {
        public ControlMapperPluginSettings controlMapperPluginSettings;
        public PluginManager PluginManager { set => throw SimHub.Plugins.PluginManager.Stub(); }
        public void Init(PluginManager pluginManager) => throw SimHub.Plugins.PluginManager.Stub();
        public void End(PluginManager pluginManager) => throw SimHub.Plugins.PluginManager.Stub();
        public virtual void SaveSettings() => throw SimHub.Plugins.PluginManager.Stub();
        public bool TriggerRole(string roleName, int durationMs) => throw SimHub.Plugins.PluginManager.Stub();
    }
}

namespace SimHub.Plugins.OutputPlugins.ControlRemapper.Models
{
    public enum OutputMode { Disabled = 0, VJoy = 1, Bridge = 2, Dummy = 3 }

    public abstract class InputMap { public string TargetRole { get => throw PluginManager.Stub(); set => throw PluginManager.Stub(); } }
    public abstract class ButtonInputMap : InputMap { }
    public class ButtonMap : ButtonInputMap { public int ButtonId { get => throw PluginManager.Stub(); set => throw PluginManager.Stub(); } }
    public class AxisToButtonInputMap : ButtonMap { }
    public class POVDirectionMap : ButtonInputMap { }
    public class VirtualButtonMap : ButtonInputMap { }

    public class ControllerMappingBase
    {
        public ObservableCollection<ButtonMap> Buttons => throw PluginManager.Stub();
        public ObservableCollection<AxisToButtonInputMap> AxisToButtons => throw PluginManager.Stub();
        public ObservableCollection<POVDirectionMap> POVDirections => throw PluginManager.Stub();
    }
    public class ControllerMapping : ControllerMappingBase { }
    public class TargetControllerMapping : ControllerMappingBase { public ObservableCollection<VirtualButtonMap> VirtualButtons => throw PluginManager.Stub(); }

    public class ControllerDescription { public string DisplayName => throw PluginManager.Stub(); }

    public class ControllerSourceMapping
    {
        public ControllerMapping ControllerMapping => throw PluginManager.Stub();
        public ControllerDescription ControllerDescription { get => throw PluginManager.Stub(); set => throw PluginManager.Stub(); }
    }

    public class OutputMapping { public TargetControllerMapping ControllerMapping => throw PluginManager.Stub(); }

    public class VJoyMapping
    {
        public int TargetVJoyId { get => throw PluginManager.Stub(); set => throw PluginManager.Stub(); }
        public bool IsVJoyInstalled => throw PluginManager.Stub();
        public int ConfiguredButtons { get => throw PluginManager.Stub(); set => throw PluginManager.Stub(); }
        public bool VJoyIsMissing => throw PluginManager.Stub();
        public bool VJoyIsBusy => throw PluginManager.Stub();
    }

    public class SimHubControllerRemapperBridgeMapping { public bool IsConnected => throw PluginManager.Stub(); }

    public class ControlMapperPluginSettings
    {
        public OutputMode OutputMode { get => throw SimHub.Plugins.PluginManager.Stub(); set => throw SimHub.Plugins.PluginManager.Stub(); }
        public ObservableCollection<ControllerSourceMapping> ControllerMappings => throw SimHub.Plugins.PluginManager.Stub();
        public OutputMapping OutputMapping => throw SimHub.Plugins.PluginManager.Stub();
        public VJoyMapping VJoyMapping => throw SimHub.Plugins.PluginManager.Stub();
        public SimHubControllerRemapperBridgeMapping SimHubControllerRemapperBridgeMapping => throw SimHub.Plugins.PluginManager.Stub();
    }
}
