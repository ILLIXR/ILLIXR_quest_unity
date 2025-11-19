# ILLIXR_quest_unity

This Unity project is a test of concept for connecting ILLIXR directly to a Unity project for the Quest 3. The project
has the following parts:

- Camera Rig (default settings)
- Cube (initially used for interaction, but is not displayed in the current setup)
  - Text Mesh Pro (used to display text sent back from ILLIXR)

## Unity Scripts

There is a single script (`Assets/Scripts/TextChanger.cs`) attached to the Cube object and interacting with the Text Mesh Pro
component. The script contains a callback function which ILLIXR can use. When starting up, the script initializes ILLIXR
and registers the callback function. In the update, the script gets the current pose from the headset and sends the pose
to ILLIXR. ILLIXR does some minimal processing and calls the callback function with the updated pose, which is then displayed
in the Text Mesh Pro object.

Commented out code lines have been removed for clarity.

```csharp
using UnityEngine;
using UnityEngine.XR;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections;
using TMPro;
using UnityEngine.Networking;
using AOT;

/*
* struct for holding the pose data
* sent to ILLIXR
* there is an equivalent struct defined in `app/src/main/cpp/include/illixr/data_format/unity_data.hpp`
*/
[StructLayout(LayoutKind.Sequential)]
public struct UnityPoseData
{
    public float positionX, positionY, positionZ;
    public float quatX, quatY, quatZ, quatW;
    public int isTracked;
}

public class TextChanger : MonoBehaviour
{
    // in order to easily have a callback the class must be a singleton
    public static TextChanger Instance;

    // object for displaying text
    public TextMeshPro myText;

    // define a callback
    public delegate void TextMeshProDelegate(string text);

    // import the set_pose function from ILLIXR, used to send the current pose to ILLIXR
    [DllImport("illixr_unity", CharSet = CharSet.Ansi)]
    private static extern void set_pose(UnityPoseData data);

    // import the ILLIXR initialization function
    [DllImport("illixr_unity", CharSet = CharSet.Ansi)]
    private static extern void initialize_for_unity([MarshalAs(UnmanagedType.LPStr)] string path, TextMeshProDelegate callback);

    [Header("Pose Tracking")]

    private InputDevice headDevice;

    // the callback function ILLIXR uses, changes the text in myText
    [MonoPInvokeCallback(typeof(TextMeshProDelegate))]
    private static void UpdateText(string text)
    {
        Debug.Log("Rx message");
        if (Instance != null)
        {
            Instance.myText.text = text;
        }
    }

    // create the singleton
    void Awake()
    {
        Instance = this;
    }
    
    void Start()
    {
        myText.text = "Initializing...";
        Debug.Log("+++===illixr startup");
        InitializeDevices();
        Debug.Log("+++===illixr data");
        // as part of the test, some assets (in Assets/StreamingAssets) are unpacked and their path is sent to ILLIXR
        StartCoroutine(ExtractAndLoadData());
    }

    void InitializeDevices()
    {
        headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        Debug.Log($"+++===Head Device: {headDevice.name}");
    }

    void Update()
    {
        // get the current pose
        if (headDevice.isValid)// && headTransform != null)
        {
            UnityPoseData data = new UnityPoseData();

            if (headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPos))
            {
                data.positionX = headPos.x;
                data.positionY = headPos.y;
                data.positionZ = headPos.z;
            }

            if (headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRot))
            {
                data.quatX = headRot.x;
                data.quatY = headRot.y;
                data.quatZ = headRot.z;
                data.quatW = headRot.w;
            }

            data.isTracked = headDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked ? 1 : 0;
            // send the pose to ILLIXR
            set_pose(data);
        }
    }

    // unpack the asset files
    IEnumerator ExtractAndLoadData()
    {
        string targetDir = Path.Combine(Application.persistentDataPath, "PluginData");

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string[] filesToExtract = { "FloorBake.png", "headset.mtl", "headset.obj", "HeadsetBake.png", "LogoBake.png",
                                    "OUTPUT_light.png", "scene.mtl", "scene.obj", "WallBake.png" };


        foreach (string fileName in filesToExtract)
        {
            string sourcePath = Path.Combine(Application.streamingAssetsPath, "PluginData", fileName);
            string targetPath = Path.Combine(targetDir, fileName);

            if (!File.Exists(targetPath))
            {
                yield return ExtractFile(sourcePath, targetPath);
            }
        }
        Debug.Log("illixr initializing");
        
        // initialize ILLIXR, sending the path to the extracted assets and the callback function
        initialize_for_unity(targetDir, UpdateText);
    }

    IEnumerator ExtractFile(string sourcePath, string targetPath)
    {
        UnityWebRequest www = UnityWebRequest.Get(sourcePath);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            File.WriteAllBytes(targetPath, www.downloadHandler.data);
            Debug.Log($"+++===Extracted: {targetPath}");
        }
        else
        {
            Debug.LogError($"+++===Failed to extract {sourcePath}: {www.error}");
        }
    }
}
```

## ILLIXR

For this project the ILLIXR code was compiled by Android Studio. The resulting libraries are in `Assets/Plugins/Android/illixr_unity.androidlib/libs/arm64-v8a`

- **libillixr_unity.so** - the main library, initializes the ILLIXR system and loads the plugins; the functions `initialize_for_unity` and `set_pose` are defined here
- **libplugin.common_lock.dbg.so** - plugin to control some resource locking; not used specifically for this project
- **libplugin.unity.pose.dbg.so** - plugin which handles the pose sent by Unity; under the hood the `set_pose` function 
  sends the pose to this plugin; when a pose is received the plugin transforms it[^1], turns it into a string, and sends
  the string back to Unity, via the callback, for display
- **libspdlog_android.so** - ILLIXR uses spdlog for logging, any messages logged will appear in the Android logcat

[^1]: The pose produced by Unity (and assumably from the headset itself) are `left-handed Y-up`, while ILLIXR uses `right-handed Y-up`. It is interesting that the pose data from Meta have this orientation, the OpenXR standard states poses should be `right-handed Y-up`, so this would seem to violate that standard.

The ILLIXR Android code used for this project can be found [here](https://github.com/ILLIXR/illixr_android) in the `quest3_unity`
branch. The files that specifically deal with the Unity interaction are (all under `app/src/main/cpp`)

- *include/illixr/data_format/unity_data.hpp* - defines the C++ version of the`UnityPoseData` struct
- *src/unity_plugin.cpp* - main entry point for Unity for ILLIXR, implements the `initialize_for_unity` function
- *src/runtime_impl.cpp* - initializes the ILLIXR system; loads and initializes the plugins; implements `set_pose` function
- *services/unity_pose/service.cpp* - plugin which receives the pose from Unity, transforms it, and sends test back to Unity
