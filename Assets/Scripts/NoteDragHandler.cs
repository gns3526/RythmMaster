using UnityEngine;

/// <summary>
/// 노트 드래그 이동 시스템
/// 마우스로 노트를 잡아서 이동시키고, 놓으면 그리드 스냅에 맞춰 배치합니다.
/// 
/// EditorController에서 이 컴포넌트를 참조하여 사용합니다.
/// </summary>
public class NoteDragHandler : MonoBehaviour
{
    static NoteDragHandler instance;
    public static NoteDragHandler Instance => instance;

    [Header("Settings")]
    public float dragThreshold = 0.1f; // 이 거리 이상 움직여야 드래그로 인식

    // 드래그 상태
    public bool isDragging = false;
    GameObject dragTarget = null;
    NoteObject dragNoteObject = null;
    Vector3 dragOffset;
    Vector3 dragStartPos;
    Vector3 mouseStartPos;
    bool isLongNoteHead = false;
    bool isLongNoteTail = false;

    // Undo용 원래 위치
    Vector3 originalPosition;
    Vector3 originalHeadPos;
    Vector3 originalTailPos;

    Camera cam;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    void Start()
    {
        cam = Camera.main;
    }

    /// <summary>
    /// 드래그 시작 시도. EditorController의 MouseBtn에서 호출.
    /// </summary>
    public bool TryStartDrag(GameObject noteObj, Vector3 worldMousePos)
    {
        if (noteObj == null) return false;

        // NoteObject 찾기
        NoteObject no = noteObj.GetComponent<NoteObject>();
        if (no == null)
            no = noteObj.GetComponentInParent<NoteObject>();

        if (no == null) return false;

        dragTarget = noteObj;
        dragNoteObject = no;
        dragStartPos = noteObj.transform.position;
        mouseStartPos = worldMousePos;
        dragOffset = noteObj.transform.position - worldMousePos;

        // 롱노트의 head/tail 판별
        if (no is NoteLong)
        {
            NoteLong longNote = no as NoteLong;
            isLongNoteHead = (noteObj == longNote.head);
            isLongNoteTail = (noteObj == longNote.tail);
            originalHeadPos = longNote.head.transform.position;
            originalTailPos = longNote.tail.transform.position;
        }
        else
        {
            isLongNoteHead = false;
            isLongNoteTail = false;
        }

        originalPosition = noteObj.transform.position;
        isDragging = false; // 아직 threshold 안 넘음

        return true;
    }

    /// <summary>
    /// 드래그 업데이트. EditorController의 Update에서 매 프레임 호출.
    /// </summary>
    public void UpdateDrag(Vector3 worldMousePos)
    {
        if (dragTarget == null) return;

        float distance = Vector3.Distance(worldMousePos, mouseStartPos);

        if (!isDragging && distance > dragThreshold)
        {
            isDragging = true;
        }

        if (isDragging)
        {
            Vector3 newPos = worldMousePos + dragOffset;

            if (dragNoteObject is NoteLong)
            {
                // 롱노트: x축은 고정 (같은 라인), y축만 이동
                if (isLongNoteHead || isLongNoteTail)
                {
                    dragTarget.transform.position = new Vector3(
                        dragTarget.transform.position.x,
                        newPos.y,
                        dragTarget.transform.position.z);

                    // 라인 렌더러 업데이트
                    NoteLong longNote = dragNoteObject as NoteLong;
                    longNote.SetPosition(new Vector3[]
                    {
                        longNote.head.transform.position,
                        longNote.tail.transform.position
                    });
                }
            }
            else
            {
                // 숏노트: 자유 이동
                dragTarget.transform.position = new Vector3(
                    SnapToLine(newPos.x),
                    newPos.y,
                    dragTarget.transform.position.z);
            }
        }
    }

    /// <summary>
    /// 드래그 종료. 그리드 스냅 적용.
    /// </summary>
    public void EndDrag()
    {
        if (dragTarget == null) return;

        if (isDragging)
        {
            // Y축 그리드 스냅
            float snapSize = 16f / Editor.Instance.Snap * 0.25f;
            float snappedY = Mathf.Round(dragTarget.transform.localPosition.y / snapSize) * snapSize;

            if (dragNoteObject is NoteLong)
            {
                NoteLong longNote = dragNoteObject as NoteLong;
                if (isLongNoteHead)
                {
                    longNote.head.transform.localPosition = new Vector3(
                        longNote.head.transform.localPosition.x,
                        Mathf.Round(longNote.head.transform.localPosition.y / snapSize) * snapSize,
                        longNote.head.transform.localPosition.z);
                }
                else if (isLongNoteTail)
                {
                    longNote.tail.transform.localPosition = new Vector3(
                        longNote.tail.transform.localPosition.x,
                        Mathf.Round(longNote.tail.transform.localPosition.y / snapSize) * snapSize,
                        longNote.tail.transform.localPosition.z);
                }
                // 라인 렌더러 최종 업데이트
                longNote.SetPosition(new Vector3[]
                {
                    longNote.head.transform.position,
                    longNote.tail.transform.position
                });
            }
            else
            {
                dragTarget.transform.localPosition = new Vector3(
                    dragTarget.transform.localPosition.x,
                    snappedY,
                    dragTarget.transform.localPosition.z);
            }

            // Undo 시스템에 기록
            if (UndoSystem.Instance != null)
            {
                UndoSystem.Instance.RecordAction(new MoveNoteAction(
                    dragNoteObject, originalPosition, dragTarget.transform.position));
            }
        }

        isDragging = false;
        dragTarget = null;
        dragNoteObject = null;
    }

    /// <summary>
    /// X 좌표를 가장 가까운 라인에 스냅
    /// </summary>
    float SnapToLine(float x)
    {
        float[] lines = NoteGenerator.Instance.linePos;
        float closest = lines[0];
        float minDist = Mathf.Abs(x - lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            float dist = Mathf.Abs(x - lines[i]);
            if (dist < minDist)
            {
                minDist = dist;
                closest = lines[i];
            }
        }
        return closest;
    }
}
