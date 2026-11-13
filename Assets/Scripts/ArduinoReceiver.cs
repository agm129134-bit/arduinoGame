using System;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class ArduinoReceiver : MonoBehaviour
{
    [Header("Serial Port Settings")]
    public string portName = "COM3";
    public int baudRate = 9600;

    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;

    [Header("Arduino Live Data")]
    public float distance = 50f;
    public Vector3 acceleration = Vector3.zero;
    public Vector2 joystick = new Vector2(500, 500);
    public bool isButtonPressed = false;

    public bool IsConnected => serialPort != null && serialPort.IsOpen;

    void Awake()
    {
        // 確保切換場景或重複加載時不會中斷
        distance = 50f;
        // 套用設定選單儲存的 COM Port (控制器分頁)
        portName = PlayerPrefs.GetString("COM_Port", portName);
        OpenConnection();
    }

    /// <summary>
    /// 以新的 COM Port 重新連線 (設定選單修改 COM Port 後呼叫)
    /// </summary>
    public void Reconnect(string newPort)
    {
        if (string.IsNullOrWhiteSpace(newPort)) return;
        CloseConnection();
        portName = newPort.Trim().ToUpperInvariant();
        distance = 50f;
        OpenConnection();
    }

    void CloseConnection()
    {
        isRunning = false;
        if (readThread != null && readThread.IsAlive)
            readThread.Join(200);
        readThread = null;

        if (serialPort != null)
        {
            try { if (serialPort.IsOpen) serialPort.Close(); } catch (Exception) { }
            serialPort = null;
        }
    }

    void OpenConnection()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 50;
            serialPort.Open();
            isRunning = true;
            readThread = new Thread(ReadSerialData);
            readThread.IsBackground = true;
            readThread.Start();
            Debug.Log($"[ArduinoReceiver] 連線成功！(Port: {portName})");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ArduinoReceiver] 未檢測到實體 Arduino 硬體 ({portName})，已自動切換至純鍵盤測試模式: {e.Message}");
        }
    }

    void ReadSerialData()
    {
        SerialPort sp = serialPort; // 區域參考，避免 Reconnect 時欄位被換掉造成 NullReference
        while (isRunning && sp != null && sp.IsOpen)
        {
            try
            {
                string data = sp.ReadLine();
                ParseData(data);
            }
            catch (TimeoutException) { }
            catch (Exception) { }
        }
    }

    void ParseData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        string[] parts = data.Split('|');
        foreach (string part in parts)
        {
            string[] kvp = part.Split(':');
            if (kvp.Length == 2)
            {
                string key = kvp[0].Trim();
                if (float.TryParse(kvp[1].Trim(), out float value))
                {
                    switch (key)
                    {
                        case "D": distance = value; break;
                        case "AX": acceleration.x = value; break;
                        case "AY": acceleration.y = value; break;
                        case "AZ": acceleration.z = value; break;
                        case "JX": joystick.x = value; break;
                        case "JY": joystick.y = value; break;
                        case "B": isButtonPressed = (value == 1); break;
                    }
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        CloseConnection();
    }

    void OnDestroy()
    {
        CloseConnection();
    }
}
