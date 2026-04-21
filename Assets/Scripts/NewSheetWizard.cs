using System.IO;
using UnityEngine;

/// <summary>
/// 신규 곡 생성 마법사
/// Sheet 폴더에 mp3(또는 wav)와 jpg가 들어있지만 .sheet 파일이 없는 폴더를 감지하여
/// BPM, Offset, 박자 정보를 입력받아 빈 .sheet 파일을 생성합니다.
/// </summary>
public class NewSheetWizard : MonoBehaviour
{
    static NewSheetWizard instance;
    public static NewSheetWizard Instance
    {
        get { return instance; }
    }

    // 입력값
    public int inputBPM = 120;
    public int inputOffset = 0;
    public int inputSignatureTop = 4;
    public int inputSignatureBottom = 4;

    // 감지된 새 곡 폴더명
    string newSongFolder = "";
    public string NewSongFolder => newSongFolder;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    /// <summary>
    /// Sheet 폴더에서 .sheet 파일이 없는 곡 폴더 찾기
    /// </summary>
    public string[] FindNewSongFolders()
    {
        string sheetPath = $"{Application.dataPath}/Sheet";
        if (!Directory.Exists(sheetPath))
        {
            Directory.CreateDirectory(sheetPath);
            return new string[0];
        }

        var result = new System.Collections.Generic.List<string>();
        DirectoryInfo di = new DirectoryInfo(sheetPath);
        foreach (DirectoryInfo d in di.GetDirectories())
        {
            // .sheet 파일이 없는 폴더 찾기
            FileInfo[] sheetFiles = d.GetFiles("*.sheet");
            if (sheetFiles.Length == 0)
            {
                // 오디오 파일이 있는지 확인
                bool hasAudio = d.GetFiles("*.mp3").Length > 0 ||
                                d.GetFiles("*.wav").Length > 0 ||
                                d.GetFiles("*.ogg").Length > 0;
                if (hasAudio)
                {
                    result.Add(d.Name);
                }
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// 새 곡 폴더 선택
    /// </summary>
    public void SelectNewSong(string folderName)
    {
        newSongFolder = folderName;
    }

    /// <summary>
    /// 새 .sheet 파일 생성
    /// </summary>
    public bool CreateSheet()
    {
        if (string.IsNullOrEmpty(newSongFolder))
        {
            Debug.LogError("곡 폴더가 선택되지 않았습니다!");
            return false;
        }

        string sheetDir = $"{Application.dataPath}/Sheet/{newSongFolder}";
        if (!Directory.Exists(sheetDir))
        {
            Debug.LogError($"폴더가 존재하지 않습니다: {sheetDir}");
            return false;
        }

        string sheetPath = $"{sheetDir}/{newSongFolder}.sheet";
        if (File.Exists(sheetPath))
        {
            Debug.LogError($".sheet 파일이 이미 존재합니다: {sheetPath}");
            return false;
        }

        // .sheet 파일 내용 작성
        string content = $"[Description]\n" +
            $"Title: {newSongFolder}\n" +
            $"Artist: Unknown\n\n" +
            $"[Audio]\n" +
            $"BPM: {inputBPM}\n" +
            $"Offset: {inputOffset}\n" +
            $"Signature: {inputSignatureTop}/{inputSignatureBottom}\n\n" +
            $"[Note]\n";

        using (StreamWriter sw = new StreamWriter(sheetPath))
        {
            sw.Write(content);
        }

        Debug.Log($"새 곡 생성 완료: {newSongFolder} (BPM: {inputBPM}, Offset: {inputOffset})");
        return true;
    }

    /// <summary>
    /// 새 곡 생성 후 에디터 모드로 진입
    /// </summary>
    public void CreateAndEdit()
    {
        if (CreateSheet())
        {
            // 새로 만든 곡을 파싱하여 sheets에 추가
            StartCoroutine(IECreateAndEdit());
        }
    }

    System.Collections.IEnumerator IECreateAndEdit()
    {
        // 새 곡 파싱
        yield return Parser.Instance.IEParse(newSongFolder);
        
        // 이미 있으면 교체, 없으면 추가
        if (GameManager.Instance.sheets.ContainsKey(newSongFolder))
            GameManager.Instance.sheets[newSongFolder] = Parser.Instance.sheet;
        else
            GameManager.Instance.sheets.Add(newSongFolder, Parser.Instance.sheet);

        // 해당 곡 선택 후 에디터 진입
        GameManager.Instance.title = newSongFolder;
        GameManager.Instance.Edit();
    }
}
