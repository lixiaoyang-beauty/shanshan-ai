using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class LearningTracker : MonoBehaviour
{
    [Header("── 配置 ──")]
    public string serverUrl = "https://shanshan-ai-production.up.railway.app";
    public string sessionId = "player1";
    public float stagnantThreshold = 45f;     // 闲置45秒才触发追问
    public float checkInterval = 2f;           // 检查闲置的间隔

    [Header("── 引用 ──")]
    public Chapter3ExperimentManager chapterManager;

    // 内部状态
    private float lastAngle = -1f;
    private float currentAngleDuration = 0f;
    private List<AngleRecord> angleHistory = new List<AngleRecord>();
    private Dictionary<string, int> wrongAnswers = new Dictionary<string, int>();
    private float lastSendTime = 0f;
    private float totalIdleTime = 0f;
    private bool wasMoving = false;
    private float lastSliderValue = -1f;
    private float lastActivityTime = 0f;

    // AI 消息队列
    private Queue<string> aiMessageQueue = new Queue<string>();
    private bool isShowingAiMessage = false;
    private bool hasLoggedStart = false;
    private float lastStagnantCheck = 0f;
    private bool stagnantAlertShown = false;

    // 全局开关：开场阶段禁止 LearningTracker 发送数据
    public static bool learningTrackerEnabled = false;

    void Start()
    {
        lastSendTime = Time.time;
        lastActivityTime = Time.time;
        if (chapterManager == null)
            chapterManager = FindObjectOfType<Chapter3ExperimentManager>();
        if (chapterManager != null)
        {
            lastSliderValue = chapterManager.GetCurrentAngle();
            lastAngle = lastSliderValue;
        }
        Debug.Log("【LearningTracker】已启动，闲置阈值：" + stagnantThreshold + "秒");
    }

    void Update()
    {
        if (!hasLoggedStart && Time.time > 0.5f)
        {
            hasLoggedStart = true;
            Debug.Log("[LearningTracker] Update开始, chapterManager=" + (chapterManager != null));
        }

        if (chapterManager == null) return;

        float currentAngle = chapterManager.GetCurrentAngle();

        // 检测滑块是否有变化（玩家真正在操作）
        bool isMovingSlider = Mathf.Abs(currentAngle - lastSliderValue) > 0.5f;

        if (isMovingSlider)
        {
            // 玩家在滑动滑块
            if (!wasMoving)
            {
                // 刚从停止变成开始移动，记录上一个停留的角度
                float angleToRecord = lastSliderValue > 0 ? lastSliderValue : lastAngle;
                if (totalIdleTime > 0.5f)
                    RecordAngleDuration(angleToRecord, totalIdleTime);
                totalIdleTime = 0f;
            }
            lastSliderValue = currentAngle;
            lastAngle = currentAngle;
            wasMoving = true;
            lastActivityTime = Time.time;
            stagnantAlertShown = false;
        }
        else
        {
            // 玩家没有动滑块，累加停留计时
            wasMoving = false;
            totalIdleTime += Time.deltaTime;
        }

        lastAngle = currentAngle;

        // 按需检查：每 checkInterval 秒检查一次闲置状态
        if (Time.time - lastStagnantCheck >= checkInterval)
        {
            lastStagnantCheck = Time.time;

            // 闲置超过阈值，且还没发过这次闲置的提示
            float idle = Time.time - lastActivityTime;
            if (learningTrackerEnabled && idle >= stagnantThreshold && !stagnantAlertShown)
            {
                stagnantAlertShown = true;
                Debug.Log("[LearningTracker] 玩家闲置超过" + stagnantThreshold + "秒，发送学习数据请求");
                SendLearningDataForStagnation();
            }
        }

        // 只在玩家重新开始滑动时才显示队列中的AI消息，且不能在等待选项时打扰
        bool isWaitingChoice = chapterManager != null && chapterManager.IsWaitingForChoice();
        if (isMovingSlider && aiMessageQueue.Count > 0 && !isShowingAiMessage && !isWaitingChoice)
        {
            ShowNextAiMessage();
        }
    }

    void RecordAngleDuration(float angle, float duration)
    {
        for (int i = 0; i < angleHistory.Count; i++)
        {
            if (Mathf.Abs(angleHistory[i].angle - angle) < 5f)
            {
                angleHistory[i].duration += duration;
                return;
            }
        }
        angleHistory.Add(new AngleRecord { angle = angle, duration = duration });
    }

    public void RecordWrongAnswer(string wrongType)
    {
        if (!wrongAnswers.ContainsKey(wrongType))
            wrongAnswers[wrongType] = 0;
        wrongAnswers[wrongType]++;
        Debug.Log("[LearningTracker] 答错上报: " + wrongType + ", 累计: " + wrongAnswers[wrongType]);
        // 答错时立即发送学习数据，触发 AI 追问
        SendLearningDataForWrongAnswer(wrongType);
    }

    public void ClearWrongAnswer(string wrongType)
    {
        if (wrongAnswers.ContainsKey(wrongType))
            wrongAnswers.Remove(wrongType);
    }

    public int GetWrongCount(string wrongType)
    {
        return wrongAnswers.TryGetValue(wrongType, out int v) ? v : 0;
    }

    // 答错时调用：发送数据并请求 AI 追问
    public void OnAnswerRecorded(string wrongType)
    {
        RecordWrongAnswer(wrongType);
    }

    // 闲置超阈值时调用
    void SendLearningDataForStagnation()
    {
        StartCoroutine(SendLearningDataCoroutine(isStagnation: true));
    }

    // 答错时调用
    void SendLearningDataForWrongAnswer(string wrongType)
    {
        StartCoroutine(SendLearningDataCoroutine(isStagnation: false, wrongType: wrongType));
    }

    IEnumerator SendLearningDataCoroutine(bool isStagnation = false, string wrongType = null)
    {
        if (chapterManager == null) yield break;

        while (angleHistory.Count > 10)
            angleHistory.RemoveAt(0);

        var data = new LearningDataPayload
        {
            session_id = sessionId,
            angle_history = angleHistory.ToArray(),
            wrong_answers = wrongAnswers,
            exploration_stage = chapterManager.GetCurrentStage(),
            current_angle = lastAngle,
            idle_time = totalIdleTime
        };

        string customJson = BuildLearningDataJson(data);

        using var req = new UnityWebRequest(serverUrl + "/learning-data", "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(customJson));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            string responseJson = req.downloadHandler.text;
            ProcessAiResponse(responseJson);
        }
    }

    string BuildLearningDataJson(LearningDataPayload data)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"session_id\":\"" + data.session_id + "\",");
        sb.Append("\"angle_history\":[");
        for (int i = 0; i < data.angle_history.Length; i++)
        {
            var r = data.angle_history[i];
            sb.Append("{\"angle\":" + r.angle.ToString("F1") + ",\"duration\":" + r.duration.ToString("F1") + "}");
            if (i < data.angle_history.Length - 1) sb.Append(",");
        }
        sb.Append("],");
        sb.Append("\"wrong_answers\":{");
        int k = 0;
        foreach (var kv in data.wrong_answers)
        {
            sb.Append("\"" + kv.Key + "\":" + kv.Value);
            if (k < data.wrong_answers.Count - 1) sb.Append(",");
            k++;
        }
        sb.Append("},");
        sb.Append("\"exploration_stage\":" + data.exploration_stage + ",");
        sb.Append("\"current_angle\":" + data.current_angle.ToString("F1") + ",");
        sb.Append("\"idle_time\":");
        sb.Append(data.idle_time.ToString("F1"));
        sb.Append("}");
        return sb.ToString();
    }

    void ProcessAiResponse(string json)
    {
        try
        {
            int idx = json.IndexOf("\"intervention\":");
            if (idx < 0) return;
            int start = json.IndexOf("\"", idx + 15);
            int end = json.IndexOf("\"", start + 1);
            if (start < 0 || end < 0) return;
            string intervention = json.Substring(start + 1, end - start - 1);

            if (intervention == "none") return;

            int msgIdx = json.IndexOf("\"message\":");
            if (msgIdx < 0) return;
            int msgStart = json.IndexOf("\"", msgIdx + 10);
            int msgEnd = json.IndexOf("\"", msgStart + 1);
            if (msgStart < 0 || msgEnd < 0) return;
            string message = json.Substring(msgStart + 1, msgEnd - msgStart - 1);

            if (!string.IsNullOrEmpty(message))
            {
                aiMessageQueue.Enqueue(message);
                Debug.Log("[LearningTracker] 收到AI干预: " + intervention + " -> " + message);
            }
        }
        catch
        {
            // 解析失败，忽略
        }
    }

    void ShowNextAiMessage()
    {
        if (aiMessageQueue.Count == 0) return;
        string msg = aiMessageQueue.Dequeue();
        isShowingAiMessage = true;
        Debug.Log("[LearningTracker] 显示AI消息: " + msg);

        if (chapterManager != null)
        {
            chapterManager.ShowAiMessage(msg, () => {
                isShowingAiMessage = false;
            });
        }
        else
        {
            isShowingAiMessage = false;
        }
    }
}

[System.Serializable]
public class AngleRecord
{
    public float angle;
    public float duration;
}

[System.Serializable]
public class LearningDataPayload
{
    public string session_id;
    public AngleRecord[] angle_history;
    public Dictionary<string, int> wrong_answers;
    public int exploration_stage;
    public float current_angle;
    public float idle_time;
}
