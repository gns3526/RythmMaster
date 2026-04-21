using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Undo/Redo 시스템
/// 노트 배치/삭제/이동 작업을 기록하고 Ctrl+Z/Y로 되돌리기/다시하기
/// </summary>
public class UndoSystem : MonoBehaviour
{
    static UndoSystem instance;
    public static UndoSystem Instance => instance;

    Stack<EditorAction> undoStack = new Stack<EditorAction>();
    Stack<EditorAction> redoStack = new Stack<EditorAction>();

    const int MAX_UNDO = 100;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    public void RecordAction(EditorAction action)
    {
        undoStack.Push(action);
        redoStack.Clear(); // 새 액션이 들어오면 redo 스택 초기화

        // 최대 개수 제한
        if (undoStack.Count > MAX_UNDO)
        {
            // Stack은 중간 삭제가 안 되므로 그냥 유지
        }
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("Undo할 작업이 없습니다.");
            return;
        }

        EditorAction action = undoStack.Pop();
        action.Undo();
        redoStack.Push(action);
        Debug.Log($"Undo: {action.GetDescription()}");
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.Log("Redo할 작업이 없습니다.");
            return;
        }

        EditorAction action = redoStack.Pop();
        action.Execute();
        undoStack.Push(action);
        Debug.Log($"Redo: {action.GetDescription()}");
    }

    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
    }

    public int UndoCount => undoStack.Count;
    public int RedoCount => redoStack.Count;
}

/// <summary>
/// 에디터 액션 베이스 클래스
/// </summary>
public abstract class EditorAction
{
    public abstract void Execute();
    public abstract void Undo();
    public abstract string GetDescription();
}

/// <summary>
/// 노트 배치 액션
/// </summary>
public class PlaceNoteAction : EditorAction
{
    NoteObject noteObject;

    public PlaceNoteAction(NoteObject note)
    {
        this.noteObject = note;
    }

    public override void Execute()
    {
        if (noteObject != null)
            noteObject.gameObject.SetActive(true);
    }

    public override void Undo()
    {
        if (noteObject != null)
            noteObject.gameObject.SetActive(false);
    }

    public override string GetDescription() => "노트 배치";
}

/// <summary>
/// 노트 삭제 액션
/// </summary>
public class DeleteNoteAction : EditorAction
{
    NoteObject noteObject;

    public DeleteNoteAction(NoteObject note)
    {
        this.noteObject = note;
    }

    public override void Execute()
    {
        if (noteObject != null)
            noteObject.gameObject.SetActive(false);
    }

    public override void Undo()
    {
        if (noteObject != null)
            noteObject.gameObject.SetActive(true);
    }

    public override string GetDescription() => "노트 삭제";
}

/// <summary>
/// 노트 이동 액션
/// </summary>
public class MoveNoteAction : EditorAction
{
    NoteObject noteObject;
    Vector3 fromPosition;
    Vector3 toPosition;

    public MoveNoteAction(NoteObject note, Vector3 from, Vector3 to)
    {
        this.noteObject = note;
        this.fromPosition = from;
        this.toPosition = to;
    }

    public override void Execute()
    {
        if (noteObject != null)
            noteObject.transform.position = toPosition;
    }

    public override void Undo()
    {
        if (noteObject != null)
            noteObject.transform.position = fromPosition;
    }

    public override string GetDescription() => "노트 이동";
}
