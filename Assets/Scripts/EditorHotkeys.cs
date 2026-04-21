using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 에디터 단축키 시스템
/// 에디터 모드에서만 동작하며, 모든 단축키를 중앙에서 관리합니다.
/// 
/// 단축키 목록:
/// ┌──────────────────────────────────────────┐
/// │ 일반                                      │
/// │  Space     : 재생/일시정지                  │
/// │  Ctrl+S    : 저장                          │
/// │  Ctrl+Z    : 실행 취소 (Undo)              │
/// │  Ctrl+Y    : 다시 실행 (Redo)              │
/// │  ESC       : 에디터 종료                    │
/// │                                            │
/// │ 노트 배치                                  │
/// │  1         : 숏노트 모드                    │
/// │  2         : 롱노트 모드                    │
/// │  F5        : 녹음 모드 시작/중지             │
/// │  Delete    : 선택된 노트 삭제               │
/// │                                            │
/// │ 탐색                                       │
/// │  Home      : 곡 처음으로                    │
/// │  End       : 곡 끝으로                      │
/// │  +/-       : 스냅 변경                      │
/// └──────────────────────────────────────────┘
/// </summary>
public class EditorHotkeys : MonoBehaviour
{
    static EditorHotkeys instance;
    public static EditorHotkeys Instance => instance;

    bool isCtrlHeld = false;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    void Update()
    {
        // 에디터 모드에서만 동작
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.state != GameManager.GameState.Edit) return;

        // 녹음 중에는 단축키 비활성화 (Space 제외)
        if (RecordMode.Instance != null && RecordMode.Instance.isRecording)
        {
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        isCtrlHeld = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;

        // === Ctrl 조합 ===
        if (isCtrlHeld)
        {
            // Ctrl+S: 저장
            if (kb.sKey.wasPressedThisFrame)
            {
                Editor.Instance.FileSave();
                Debug.Log("💾 저장 완료 (Ctrl+S)");
            }

            // Ctrl+Z: Undo
            if (kb.zKey.wasPressedThisFrame)
            {
                if (UndoSystem.Instance != null)
                    UndoSystem.Instance.Undo();
            }

            // Ctrl+Y: Redo
            if (kb.yKey.wasPressedThisFrame)
            {
                if (UndoSystem.Instance != null)
                    UndoSystem.Instance.Redo();
            }

            return; // Ctrl 조합 시 다른 단축키 무시
        }

        // === 단일 키 ===

        // 1: 숏노트 모드
        if (kb.digit1Key.wasPressedThisFrame)
        {
            Editor.Instance.SelectShortNote();
            Debug.Log("🔵 숏노트 모드");
        }

        // 2: 롱노트 모드
        if (kb.digit2Key.wasPressedThisFrame)
        {
            Editor.Instance.SelectLongNote();
            Debug.Log("🟢 롱노트 모드");
        }

        // F5: 녹음 모드
        if (kb.f5Key.wasPressedThisFrame)
        {
            if (RecordMode.Instance != null)
            {
                if (RecordMode.Instance.isRecording || RecordMode.Instance.isCountdown)
                {
                    RecordMode.Instance.StopRecording();
                    Debug.Log("⏹ 녹음 중지 (F5)");
                }
                else
                {
                    RecordMode.Instance.StartRecording();
                    Debug.Log("🔴 녹음 시작 (F5)");
                }
            }
        }

        // Delete: 선택된 노트 삭제
        if (kb.deleteKey.wasPressedThisFrame)
        {
            DeleteSelectedNote();
        }

        // Home: 곡 처음으로
        if (kb.homeKey.wasPressedThisFrame)
        {
            GoToStart();
        }

        // End: 곡 끝으로
        if (kb.endKey.wasPressedThisFrame)
        {
            GoToEnd();
        }

        // +/-: 스냅 변경
        if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
        {
            Editor.Instance.Snap /= 2;
            EditorController.Instance.GridSnapListener?.Invoke(Editor.Instance.Snap);
            Debug.Log($"스냅: {Editor.Instance.Snap}");
        }
        if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
        {
            Editor.Instance.Snap *= 2;
            EditorController.Instance.GridSnapListener?.Invoke(Editor.Instance.Snap);
            Debug.Log($"스냅: {Editor.Instance.Snap}");
        }
    }

    void DeleteSelectedNote()
    {
        // EditorController의 selectedNoteObject 사용
        if (EditorController.Instance == null) return;

        GameObject selected = EditorController.Instance.selectedNoteObject;
        if (selected != null)
        {
            NoteObject noteObj = selected.GetComponent<NoteObject>();
            if (noteObj == null)
                noteObj = selected.GetComponentInParent<NoteObject>();

            if (noteObj != null)
            {
                // Undo 기록
                if (UndoSystem.Instance != null)
                    UndoSystem.Instance.RecordAction(new DeleteNoteAction(noteObj));

                if (noteObj is NoteLong)
                    noteObj.gameObject.SetActive(false);
                else
                    selected.SetActive(false);

                Debug.Log("🗑 노트 삭제 (Delete)");
            }
        }
    }

    void GoToStart()
    {
        AudioManager.Instance.progressTime = 0f;
        Editor.Instance.objects.transform.position = new Vector3(0f, Editor.Instance.offsetPosition, 0f);
        Editor.Instance.CalculateCurrentBar();
        Debug.Log("⏮ 곡 처음으로 (Home)");
    }

    void GoToEnd()
    {
        float endTime = AudioManager.Instance.Length - 0.1f;
        AudioManager.Instance.progressTime = endTime;

        float barPerTime = GameManager.Instance.sheets[GameManager.Instance.title].BarPerSec;
        float pos = endTime / barPerTime * 16;
        Editor.Instance.objects.transform.position = new Vector3(0f, -pos + Editor.Instance.offsetPosition, 0f);
        Editor.Instance.CalculateCurrentBar();
        Debug.Log("⏭ 곡 끝으로 (End)");
    }
}
