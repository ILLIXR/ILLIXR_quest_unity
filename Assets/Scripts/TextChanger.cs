using UnityEngine;
using UnityEngine.XR;
//using UnityEngine.XR.OpenXR;
//using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections;
using TMPro;
using UnityEngine.Networking;
using AOT;

[StructLayout(LayoutKind.Sequential)]
public struct UnityPoseData
{
    public float positionX, positionY, positionZ;
    public float quatX, quatY, quatZ, quatW;
    public int isTracked;
}

public class TextChanger : MonoBehaviour
{
    public static TextChanger Instance;

    public TextMeshPro myText;

    public delegate void TextMeshProDelegate(string text);

    [DllImport("illixr_unity", CharSet = CharSet.Ansi)]
    private static extern void set_pose(UnityPoseData data);

    [DllImport("illixr_unity", CharSet = CharSet.Ansi)]
    private static extern void initialize_for_unity([MarshalAs(UnmanagedType.LPStr)] string path, TextMeshProDelegate callback);

    [Header("Pose Tracking")]
    //public Transform headTransform;
    //public Transform leftControllerTransform;
    //public Transform rightControllerTransform;

    //[Header("Passthrough")]
    //public bool enablePassthrough = true;

    private InputDevice headDevice;

    [MonoPInvokeCallback(typeof(TextMeshProDelegate))]
    private static void UpdateText(string text)
    {
        Debug.Log("Rx message");
        if (Instance != null)
        {
            Instance.myText.text = text;
        }
    }

    void Awake()
    {
        Instance = this;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        myText.text = "Initializing...";
        Debug.Log("+++===illixr startup");
        InitializeDevices();
        Debug.Log("+++===illixr data");
        StartCoroutine(ExtractAndLoadData());

        //SetupPassthrough();   
    }

    void InitializeDevices()
    {
        // Get XR devices
        headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        //leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        //rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        Debug.Log($"+++===Head Device: {headDevice.name}");
        //Debug.Log($"Left Controller: {leftController.name}");
        //Debug.Log($"Right Controller: {rightController.name}");
    }

    // Update is called once per frame
    void Update()
    {
        // Update head pose
        if (headDevice.isValid)// && headTransform != null)
        {
            UnityPoseData data = new UnityPoseData();

            if (headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPos))
            {
                data.positionX = headPos.x;
                data.positionY = headPos.y;
                data.positionZ = headPos.z;
                //headTransform.localPosition = headPos;
            }

            if (headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRot))
            {
                data.quatX = headRot.x;
                data.quatY = headRot.y;
                data.quatZ = headRot.z;
                data.quatW = headRot.w;
                //headTransform.localRotation = headRot;
            }

            data.isTracked = headDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked ? 1 : 0;
//#if UNITY_ANDROID && !UNITY_EDITOR
            set_pose(data);
//#endif

        }
        // Update left controller pose
        //if (leftController.isValid && leftControllerTransform != null)
        //{
        //    if (leftController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 leftPos))
        //    {
        //        leftControllerTransform.localPosition = leftPos;
        //    }

        //    if (leftController.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion leftRot))
        //    {
        //        leftControllerTransform.localRotation = leftRot;
        //    }
        //}

        // Update right controller pose
        //if (rightController.isValid && rightControllerTransform != null)
        //{
        //    if (rightController.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 rightPos))
        //    {
        //        rightControllerTransform.localPosition = rightPos;
        //    }

        //    if (rightController.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rightRot))
        //    {
        //        rightControllerTransform.localRotation = rightRot;
        //    }
        //}

    }

    IEnumerator ExtractAndLoadData()
    {
        // create persistent storage
        string targetDir = Path.Combine(Application.persistentDataPath, "PluginData");

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // get the files to extract
        string[] filesToExtract = { "FloorBake.png", "headset.mtl", "headset.obj", "HeadsetBake.png", "LogoBake.png",
                                    "OUTPUT_light.png", "scene.mtl", "scene.obj", "WallBake.png" };


        foreach (string fileName in filesToExtract)
        {
            string sourcePath = Path.Combine(Application.streamingAssetsPath, "PluginData", fileName);
            string targetPath = Path.Combine(targetDir, fileName);

            // check if the file already exists
            if (!File.Exists(targetPath))
            {
                yield return ExtractFile(sourcePath, targetPath);
            }
        }
        Debug.Log("illixr initializing");
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
