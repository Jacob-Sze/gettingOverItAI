using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using BepInEx;

[BepInPlugin("com.yourname.goi.timercheckpoint", "GOI Timer & Checkpoints", "1.0.0")]
public class TimerCheckpointMod : BaseUnityPlugin
{
    private GUIStyle timerStyle;

    // Player & Camera references
    private PlayerControl playerControl;
    private GameObject playerRoot;
    private Rigidbody2D[] playerBodies;
    private Camera gameCamera;

    // Checkpoint variables
    private Vector3[] savedPositions;
    private float[] savedRotations;
    private bool hasCheckpoint = false;

    // Fly hack variables
    private bool isFlying = false;
    private float flySpeed = 15f;
    private float boostMultiplier = 3f;

    void Start()
    {
        timerStyle = new GUIStyle();
        timerStyle.fontSize = 16;
        timerStyle.normal.textColor = Color.yellow;
    }

    void Update()
    {
        if (playerRoot == null)
        {
            playerControl = FindObjectOfType<PlayerControl>();
            if (playerControl != null)
            {
                playerRoot = playerControl.gameObject;
                playerBodies = playerRoot.GetComponentsInChildren<Rigidbody2D>();
                EnableTrackpadMode(playerControl);
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (gameCamera == null && playerBodies != null && playerBodies.Length > 0)
        {
            gameCamera = FindGameplayCamera(playerBodies[0].position);
        }

        WritePythonData();

        // Hotkey: F3 to Toggle Fly Hack
        if (UnityEngine.Input.GetKeyDown(KeyCode.F3) && playerBodies != null)
        {
            ToggleFly();
        }

        // Execute Fly Movement if active
        if (isFlying && playerBodies != null)
        {
            HandleFlyMovement();
        }

        // Hotkey: F5 to Save Checkpoint
        if (UnityEngine.Input.GetKeyDown(KeyCode.F5) && playerBodies != null)
        {
            SaveCheckpoint();
        }

        // Hotkey: F6 to Load Checkpoint
        if (UnityEngine.Input.GetKeyDown(KeyCode.F6) && hasCheckpoint && playerBodies != null)
        {
            LoadCheckpoint();
        }
    }

    private void EnableTrackpadMode(PlayerControl pc)
    {
        // Set trackpad = true via reflection (works regardless of public/private field visibility)
        FieldInfo trackpadField = typeof(PlayerControl).GetField(
            "trackpad",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (trackpadField != null)
        {
            trackpadField.SetValue(pc, true);
        }
        PlayerPrefs.SetInt("Trackpad", 1);
    }

    private Camera FindGameplayCamera(Vector3 potWorldPos)
    {
        // 1. Try grabbing the camera from GOI's CameraControl script first
        var camCtrl = FindObjectOfType<CameraControl>();
        if (camCtrl != null)
        {
            Camera c = camCtrl.GetComponent<Camera>();
            if (c != null) return c;
            c = camCtrl.GetComponentInChildren<Camera>();
            if (c != null) return c;
        }

        // 2. Fallback: find the active camera where the pot is actually centered inside the viewport [0..1]
        foreach (Camera c in Camera.allCameras)
        {
            if (!c.isActiveAndEnabled) continue;
            Vector3 vp = c.WorldToViewportPoint(potWorldPos);
            if (vp.z > 0 && vp.x >= 0.05f && vp.x <= 0.95f && vp.y >= 0.05f && vp.y <= 0.95f)
            {
                return c;
            }
        }

        return Camera.main;
    }

    private void ToggleFly()
    {
        isFlying = !isFlying;

        for (int i = 0; i < playerBodies.Length; i++)
        {
            playerBodies[i].velocity = Vector2.zero;
            playerBodies[i].angularVelocity = 0f;
            playerBodies[i].gravityScale = isFlying ? 0f : 1f;
        }

        Logger.LogInfo("Fly Mode: " + (isFlying ? "ON" : "OFF"));
    }

    private void HandleFlyMovement()
    {
        Vector3 moveDir = Vector3.zero;

        if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
            moveDir.y += 1f;
        if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
            moveDir.y -= 1f;
        if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
            moveDir.x += 1f;
        if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
            moveDir.x -= 1f;

        if (moveDir != Vector3.zero)
        {
            float currentSpeed = flySpeed;
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift))
            {
                currentSpeed *= boostMultiplier;
            }

            Vector3 delta = moveDir.normalized * currentSpeed * Time.deltaTime;

            for (int i = 0; i < playerBodies.Length; i++)
            {
                playerBodies[i].transform.position += delta;
                playerBodies[i].position += (Vector2)delta;
            }
        }
    }

    private void SaveCheckpoint()
    {
        savedPositions = new Vector3[playerBodies.Length];
        savedRotations = new float[playerBodies.Length];

        for (int i = 0; i < playerBodies.Length; i++)
        {
            savedPositions[i] = playerBodies[i].transform.position;
            savedRotations[i] = playerBodies[i].rotation;
        }

        hasCheckpoint = true;
        Logger.LogInfo("Checkpoint saved!");
    }

    private void WritePythonData()
    {
        string filePath = @"C:\Users\jacob\Documents\CS-530\storage.txt";

        try
        {
            if (playerBodies == null || playerBodies.Length == 0 || gameCamera == null) return;

            // Project index 0 (pot) using the verified gameplay camera
            Vector3 worldPos = playerBodies[1].position;
            Vector3 screenPos = gameCamera.WorldToScreenPoint(worldPos);

            // Top-left client window pixel coordinates
            float screenX = screenPos.x;
            float screenY = Screen.height - screenPos.y;

            string customText = $"{screenX:F2}, {screenY:F2}";
            File.WriteAllText(filePath, customText);
        }
        catch (System.Exception e)
        {
            Logger.LogWarning("Could not write pot screen position to storage file: " + e.Message);
        }
    }

    private void LoadCheckpoint()
    {
        for (int i = 0; i < playerBodies.Length; i++)
        {
            playerBodies[i].velocity = Vector2.zero;
            playerBodies[i].angularVelocity = 0f;

            playerBodies[i].transform.position = savedPositions[i];
            playerBodies[i].position = savedPositions[i];
            playerBodies[i].rotation = savedRotations[i];
        }

        Logger.LogInfo("Checkpoint loaded!");
    }

    void OnGUI()
    {
        string controls = $"[F1] Timer  [F2] Reset  [F3] Fly ({(isFlying ? "ON" : "OFF")})  [F5] Save Pos  [F6] Load Pos";
        GUI.Label(new Rect(20, 20, 600, 30), controls);

        if (playerBodies == null || gameCamera == null) return;
    }
}