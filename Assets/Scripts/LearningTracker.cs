using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class LearningTracker : MonoBehaviour
{
    [Header("── 配置 ──")]
    public string serverUrl = "http://localhost:8080";
    public string sessionId = "player1";
    public float sendInterval = 5f;        // 每隔多少秒发送一次数据
    public float stagnantThreshold = 20f;   // 停滞多少秒算迷思

    [Header("── 引用 ──")]
    public Chapter3ExperimentManager chapterManager;

    // 内部状态
    private float lastAngle = -1f;
    private float currentAngleDuration = 0f;  // 当前角度停留时间
    private List<AngleRecord> angleHistory = new List<AngleRecord>();
    private Dictionary<string, int> wrongAnswers = new Dictionary<string, int>();
    private float lastSendTime = 0f;
    private float idleTime = 0f;
    private float lastSliderValue = -1f;
    private bool isSliding = false;

    // AI 消息队列（用于自然节点显示）
    private Queue<string> aiMessageQueue = new Queue<string>();
    private bool isShowingAiMessage = false;

    void Start()
    {
        lastSendTime = Time.time;
    }

    void Update()
    {
        if (chapterManager == null) return;

        // 获取当前入射角
        float currentAngle = chapterManager.GetCurrentAngle();

        // 角度变化检测
        if (Mathf.Abs(currentAngle - lastSliderValue) > 0.5f)
        {
            // 玩家在滑动
            if (lastSliderValue > 0)
            {
                // 记录上一个角度的停留时间
                RecordAngleDuration(lastSliderValue, currentAngleDuration);
            }
            lastSliderValue = currentAngle;
            currentAngleDuration = 0f;
            isSliding = true;
        }
        else
        {
            // 停止滑动，累加停留时间
            if (isSliding)
            {
                idleTime = 0f;
            }
            currentAngleDuration += Time.deltaTime;
            idleTime += Time.deltaTime;
            isSliding = false;
        }

        lastAngle = currentAngle;

        // 定时发送数据
        if (Time.time - lastSendTime >= sendInterval)
        {
            SendLearningData();
            lastSendTime = Time.time;
        }

        // 自然节点检查：玩家停止滑动超过3秒，且队列有待显示消息
        if (!isSliding && idleTime > 3f && aiMessageQueue.Count > 0 && !isShowingAiMessage)
        {
            ShowNextAiMessage();
        }
    }

    void RecordAngleDuration(float angle, float duration)
    {
        // 合并相邻近的角度记录（<5度范围合并）
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
    }

    public void ClearWrongAnswer(string wrongType)
    {
        // 答对时从记录中移除该类型（可选）
        if (wrongAnswers.ContainsKey(wrongType))
            wrongAnswers.Remove(wrongType);
    }

    public int GetWrongCount(string wrongType)
    {
        return wrongAnswers.TryGetValue(wrongType, out int v) ? v : 0;
    }

    void SendLearningData()
    {
        // 清理过旧的角度记录（只保留最近10条）
        while (angleHistory.Count > 10)
            angleHistory.RemoveAt(0);

        StartCoroutine(SendLearningDataCoroutine());
    }

    IEnumerator SendLearningDataCoroutine()
    {
        var data = new LearningDataPayload
        {
            session_id = sessionId,
            angle_history = angleHistory.ToArray(),
            wrong_answers = wrongAnswers,
            exploration_stage = chapterManager.GetCurrentStage(),
            current_angle = lastAngle,
            idle_time = idleTime
        };

        string json = JsonUtility.ToJson(data);
        // Unity 的 JsonUtility 不支持 Dictionary，需要手动构建
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
        // API 失败不影响游戏，忽略
    }

    string BuildLearningDataJson(LearningDataPayload data)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"session_id\":\"{data.session_id}\",");
        sb.Append($"\"angle_history\":[");
        for (int i = 0; i < data.angle_history.Length; i++)
        {
            var r = data.angle_history[i];
            sb.Append($"{{\"angle\":{r.angle:F1},\"duration\":{r.duration:F1}}}");
            if (i < data.angle_history.Length - 1) sb.Append(",");
        }
        sb.Append("],");
        sb.Append("\"wrong_answers\":{");
        int k = 0;
        foreach (var kv in data.wrong_answers)
        {
            sb.Append($"\"{kv.Key}\":{kv.Value}");
            if (k < data.wrong_answers.Count - 1) sb.Append(",");
            k++;
        }
        sb.Append("},");
        sb.Append($"\"exploration_stage\":{data.exploration_stage},");
        sb.Append($"\"current_angle\":{data.current_angle:F1},");
        sb.Append($"\"idle_time\":{data.idle_time:F1}}");
        return sb.ToString();
    }

    void ProcessAiResponse(string json)
    {
        try
        {
            // 简单解析 JSON（避免依赖外部库）
            int idx = json.IndexOf("\"intervention\":");
            if (idx < 0) return;
            int start = json.IndexOf("\"", idx + 15);
            int end = json.IndexOf("\"", start + 1);
            string intervention = json.Substring(start + 1, end - start - 1);

            if (intervention == "none") return;

            // 提取 message
            int msgIdx = json.IndexOf("\"message\":");
            if (msgIdx < 0) return;
            int msgStart = json.IndexOf("\"", msgIdx + 10);
            int msgEnd = json.IndexOf("\"", msgStart + 1);
            if (msgStart < 0 || msgEnd < 0) return;
            string message = json.Substring(msgStart + 1, msgEnd - msgStart - 1);

            if (!string.IsNullOrEmpty(message))
            {
                aiMessageQueue.Enqueue(message);
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

    // 外部调用：答题后调用，触发一次数据发送
    public void OnAnswerRecorded(string wrongType)
    {
        RecordWrongAnswer(wrongType);
        SendLearningData();
        lastSendTime = Time.time;
    }
}

// 数据结构
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
