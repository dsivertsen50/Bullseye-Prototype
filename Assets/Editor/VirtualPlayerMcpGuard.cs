using System.IO;
using Unity.AI.MCP.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Multiplayer Play Mode refuses to open a virtual-player window whose layout
/// contains the MCP connection dialog. Keep that dialog out of cloned editors.
/// </summary>
[InitializeOnLoad]
static class VirtualPlayerMcpGuard
{
    const string DialogTypeName = "Unity.AI.MCP.Editor.UI.ConnectionApprovalDialog";

    static VirtualPlayerMcpGuard()
    {
        if (!IsVirtualPlayer())
            return;

        RepairPlayerLayout();
        EditorApplication.update += SuppressConnectionDialog;
    }

    static void SuppressConnectionDialog()
    {
        if (UnityMCPBridge.IsRunning)
            UnityMCPBridge.Stop();

        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            EditorWindow window = windows[i];
            if (window != null && window.GetType().FullName == DialogTypeName)
                window.Close();
        }
    }

    static void RepairPlayerLayout()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string layoutPath = Path.Combine(projectRoot, "Library", "UserSettings", "Layouts", "layout_0010.wlt");
        if (!File.Exists(layoutPath))
            return;

        string current = File.ReadAllText(layoutPath);
        if (!current.Contains(DialogTypeName))
            return;

        string title = "Player";
        const string titleKey = "m_Title: ";
        int titleIndex = current.IndexOf(titleKey, System.StringComparison.Ordinal);
        if (titleIndex >= 0)
        {
            int start = titleIndex + titleKey.Length;
            int end = current.IndexOf('\n', start);
            if (end > start)
                title = current.Substring(start, end - start).Trim();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath));
        File.WriteAllText(layoutPath, GameViewLayout(title));
    }

    static bool IsVirtualPlayer()
    {
        string path = Application.dataPath.Replace('\\', '/');
        return path.Contains("/Library/VP/");
    }

    static string GameViewLayout(string title)
    {
        return
@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &1
MonoBehaviour:
  m_ObjectHideFlags: 52
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 12004, guid: 0000000000000000e000000000000000, type: 0}
  m_Name: 
  m_EditorClassIdentifier: UnityEditor.dll::UnityEditor.ContainerWindow
  m_PixelRect:
    serializedVersion: 2
    x: 1
    y: 31
    width: 1280
    height: 720
  m_ShowMode: 0
  m_Title: " + title + @"
  m_RootView: {fileID: 3}
  m_MinSize: {x: 200, y: 200}
  m_MaxSize: {x: 4000, y: 4000}
  m_Maximized: 0
--- !u!114 &2
MonoBehaviour:
  m_ObjectHideFlags: 52
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 12006, guid: 0000000000000000e000000000000000, type: 0}
  m_Name: GameView
  m_EditorClassIdentifier: UnityEditor.dll::UnityEditor.DockArea
  m_Children: []
  m_Position:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1280
    height: 720
  m_MinSize: {x: 200, y: 200}
  m_MaxSize: {x: 4000, y: 4000}
  m_ActualView: {fileID: 4}
  m_Panes:
  - {fileID: 4}
  m_Selected: 0
  m_LastSelected: 0
--- !u!114 &3
MonoBehaviour:
  m_ObjectHideFlags: 52
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 12010, guid: 0000000000000000e000000000000000, type: 0}
  m_Name: 
  m_EditorClassIdentifier: UnityEditor.dll::UnityEditor.SplitView
  m_Children:
  - {fileID: 2}
  m_Position:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1280
    height: 720
  m_MinSize: {x: 200, y: 200}
  m_MaxSize: {x: 4000, y: 4000}
  vertical: 0
  controlID: 0
  draggingID: 0
--- !u!114 &4
MonoBehaviour:
  m_ObjectHideFlags: 52
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 1
  m_Script: {fileID: 12015, guid: 0000000000000000e000000000000000, type: 0}
  m_Name: 
  m_EditorClassIdentifier: 
  m_MinSize: {x: 200, y: 200}
  m_MaxSize: {x: 4000, y: 4000}
  m_TitleContent:
    m_Text: Game
    m_Image: {fileID: 0}
    m_Tooltip: 
  m_Pos:
    serializedVersion: 2
    x: 0
    y: 26
    width: 1280
    height: 694
  m_ViewDataDictionary: {fileID: 0}
  m_SerializedViewsNames: []
  m_SerializedViewsValues: []
  m_PlayModeViewName: GameView
  m_ShowGizmos: 0
  m_TargetDisplay: 0
  m_ClearColor: {r: 0, g: 0, b: 0, a: 0}
  m_TargetSize: {x: 1280, y: 720}
  m_TextureFilterMode: 0
  m_TextureHideFlags: 61
  m_RenderIMGUI: 1
  m_MaximizeOnPlay: 0
  m_UseMipMap: 0
  m_VSyncEnabled: 0
  m_Gizmos: 0
  m_Stats: 0
  m_SelectedSizes: 00000000000000000000000000000000000000000000000000000000000000000000000000000000
  m_ZoomArea:
    m_HRangeLocked: 0
    m_VRangeLocked: 0
    hZoomLockedByDefault: 0
    vZoomLockedByDefault: 0
    m_HBaseRangeMin: -640
    m_HBaseRangeMax: 640
    m_VBaseRangeMin: -360
    m_VBaseRangeMax: 360
    m_HAllowExceedBaseRangeMin: 1
    m_HAllowExceedBaseRangeMax: 1
    m_VAllowExceedBaseRangeMin: 1
    m_VAllowExceedBaseRangeMax: 1
    m_ScaleWithWindow: 0
    m_HSlider: 0
    m_VSlider: 0
    m_IgnoreScrollWheelUntilClicked: 0
    m_EnableMouseInput: 1
    m_EnableSliderZoomHorizontal: 0
    m_EnableSliderZoomVertical: 0
    m_UniformScale: 1
    m_UpDirection: 1
    m_DrawArea:
      serializedVersion: 2
      x: 0
      y: 0
      width: 1280
      height: 720
    m_Scale: {x: 1, y: 1}
    m_Translation: {x: 640, y: 360}
    m_MarginLeft: 0
    m_MarginRight: 0
    m_MarginTop: 0
    m_MarginBottom: 0
    m_LastShownAreaInsideMargins:
      serializedVersion: 2
      x: -640
      y: -360
      width: 1280
      height: 720
    m_MinimalGUI: 0
";
    }
}
