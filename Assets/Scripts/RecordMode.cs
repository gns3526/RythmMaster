using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 녹음 모드: 곡 재생 중 QWER 키 입력으로 노트를 실시간 배치
/// 
/// 사용법:
/// 1. 에디터에서 F5 또는 REC 버튼 클릭
/// 2. 3초 카운트다운 후 곡 재생 시작
/// 3. QWER 키를 누르면 해당 시간에 노트 자동 생성
///    - 짧게 누르기 → 숏노트
///    - 길게 누르기 → 롱노트 (키 누른 시간 ~ 뗀 시간)
/// 4. 곡 끝나면 녹음 완료
/// </summary>
public class RecordMode : MonoBehaviour
{
    static RecordMode instance;
    public static RecordMode Instance => instance;

    public bool isRecording = false;
    public bool isCountdown = false;

    // 키별 롱노트 트래킹
    float[] keyPressTime = new float[4] { -1, -1, -1, -1 };
    bool[] keyHeld = new bool[4];

    // 녹음된 노트 임시 저장
    List<Note> recordedNotes = new List<Note>();

    // 카운트다운
    int countdownValue = 3;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    /// <summary>
    /// 녹음 시작 (카운트다운 포함)
    /// </summary>
    public void StartRecording()
    {
        if (isRecording) return;

        recordedNotes.Clear();
        keyPressTime = new float[4] { -1, -1, -1, -1 };
        keyHeld = new bool[4];

        // 곡 처음으로
        AudioManager.Instance.progressTime = 0f;
        Editor.Instance.objects.transform.position = new Vector3(0f, Editor.Instance.offsetPosition, 0f);

        StartCoroutine(IECountdownAndRecord());
    }

    IEnumerator IECountdownAndRecord()
    {
        isCountdown = true;

        // 3초 카운트다운
        for (countdownValue = 3; countdownValue > 0; countdownValue--)
        {
            Debug.Log($"Recording starts in: {countdownValue}");
            yield return new WaitForSeconds(1f);
        }

        isCountdown = false;
        isRecording = true;

        // 곡 재생
        Editor.Instance.Play();
        Debug.Log("🔴 녹음 시작!");

        // 곡이 끝날 때까지 대기
        while (AudioManager.Instance.IsPlaying())
        {
            yield return null;
        }

        StopRecording();
    }

    /// <summary>
    /// 녹음 중 키 입력 처리 (InputManager에서 호출)
    /// </summary>
    public void OnNoteKeyPressed(int line)
    {
        if (!isRecording) return;

        float currentTime = AudioManager.Instance.GetMilliSec();
        keyPressTime[line] = currentTime;
        keyHeld[line] = true;
    }

    /// <summary>
    /// 녹음 중 키 릴리즈 처리 (InputManager에서 호출)
    /// </summary>
    public void OnNoteKeyReleased(int line)
    {
        if (!isRecording || !keyHeld[line]) return;

        float releaseTime = AudioManager.Instance.GetMilliSec();
        float pressTime = keyPressTime[line];
        keyHeld[line] = false;

        if (pressTime < 0) return;

        float duration = releaseTime - pressTime;

        if (duration < 200f) // 200ms 미만이면 숏노트
        {
            Note note = new Note((int)pressTime, (int)NoteType.Short, line + 1, -1);
            recordedNotes.Add(note);

            // 실시간으로 화면에 노트 표시
            Vector3 pos = CalculateNotePosition(pressTime, line);
            NoteGenerator.Instance.DisposeNoteShort(NoteType.Short, pos);
        }
        else // 200ms 이상이면 롱노트
        {
            Note note = new Note((int)pressTime, (int)NoteType.Long, line + 1, (int)releaseTime);
            recordedNotes.Add(note);

            // 실시간으로 화면에 롱노트 표시
            Vector3 headPos = CalculateNotePosition(pressTime, line);
            Vector3 tailPos = CalculateNotePosition(releaseTime, line);
            NoteGenerator.Instance.DisposeNoteLong(0, new Vector3[] { headPos, tailPos });
            NoteGenerator.Instance.DisposeNoteLong(1, new Vector3[] { headPos, tailPos });
        }

        keyPressTime[line] = -1;
    }

    /// <summary>
    /// 시간(ms)과 라인으로 에디터 내 노트 위치 계산
    /// </summary>
    Vector3 CalculateNotePosition(float timeMs, int line)
    {
        Sheet sheet = GameManager.Instance.sheets[GameManager.Instance.title];
        float baseTime = sheet.BarPerSec / 16f;
        float yPos = (timeMs - sheet.offset) / 1000f / baseTime;
        return new Vector3(NoteGenerator.Instance.linePos[line], yPos * 0.25f, 0f);
    }

    /// <summary>
    /// 녹음 중지
    /// </summary>
    public void StopRecording()
    {
        if (!isRecording && !isCountdown) return;

        isRecording = false;
        isCountdown = false;

        if (AudioManager.Instance.IsPlaying())
        {
            Editor.Instance.Play(); // 토글이므로 정지됨
        }

        Debug.Log($"⏹ 녹음 완료! 총 {recordedNotes.Count}개 노트 녹음됨");
    }

    /// <summary>
    /// 녹음 결과를 sheet에 반영
    /// </summary>
    public void AcceptRecording()
    {
        Sheet sheet = GameManager.Instance.sheets[GameManager.Instance.title];
        foreach (Note note in recordedNotes)
        {
            sheet.notes.Add(note);
        }
        // 시간순 정렬
        sheet.notes.Sort((a, b) => a.time.CompareTo(b.time));
        recordedNotes.Clear();
        Debug.Log("녹음 결과가 반영되었습니다.");
    }

    /// <summary>
    /// 녹음 결과 버리기
    /// </summary>
    public void DiscardRecording()
    {
        recordedNotes.Clear();
        Debug.Log("녹음 결과가 폐기되었습니다.");
    }

    public int GetCountdown() => countdownValue;
    public int GetRecordedCount() => recordedNotes.Count;
}
