using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private static GameManager instance = null;

    public Dungeon dungeon = null;
    public Player player = null;

    private bool hasFoundExit = false;

    // UI 관련 변수
    private Canvas canvas = null;
    private Text timerText = null;
    private float remainingTime = 60f;  // 1분 = 60초
    private bool isTimerRunning = true;
    private float blinkInterval = 0.3f;  // 깜빡임 간격 (초)
    private float blinkTimer = 0f;  // 깜빡임 타이머

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("GameManager");
                    instance = obj.AddComponent<GameManager>();
                }
            }
            return instance;
        }
    }

    private void Start()
    {
        // 마우스 커서 잠금 및 숨김 설정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // UI Canvas 생성
        CreateCanvas();
        Debug.Log("Canvas created in Start()");
        // 타이머 UI 생성
        CreateTimerUI();
        Debug.Log("Timer UI created in Start()");

        // Main 카메라에서 CameraOcclusion 컴포넌트 획득 및 Clear
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            CameraOcclusion cameraOcclusion = mainCamera.GetComponent<CameraOcclusion>();
            if (cameraOcclusion != null)
            {
                cameraOcclusion.Clear();
                Debug.Log("CameraOcclusion cleared in Start()");
            }
        }

        dungeon.Generate();
        Debug.Log("Dungeon generated in Start()");
    }

    private void Update()
    {
        CheckExitReached();
        UpdateTimer();
    }

    private void HandleInput()
    {
        // ESC 키: 마우스 락 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMouseLock();
            return;
        }
    }

    /// <summary>
    /// 마우스 락 상태를 토글합니다 (ESC 키로 실행).
    /// </summary>
    void ToggleMouseLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("Mouse unlocked");
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("Mouse locked");
        }
    }


    /// <summary>
    /// Canvas를 생성합니다.
    /// </summary>
    private Canvas CreateCanvas()
    {
        // 기존 Canvas가 있으면 사용
        canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        // 새로운 Canvas 생성
        GameObject canvasObj = new GameObject("UICanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // CanvasScaler 추가
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // GraphicRaycaster 추가
        canvasObj.AddComponent<GraphicRaycaster>();

        Debug.Log("Canvas created successfully!");
        return canvas;
    }

    /// <summary>
    /// Canvas에 Text UI를 추가합니다.
    /// </summary>
    public Text AddTextUI(string name, string initialText, Vector2 position, Vector2 sizeDelta, 
                         int fontSize = 30, TextAnchor alignment = TextAnchor.UpperLeft, 
                         Color? color = null)
    {
        if (canvas == null)
        {
            Debug.LogError("Canvas is not created. Call CreateCanvas() first.");
            return null;
        }

        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(canvas.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.text = initialText;
        text.color = color ?? Color.white;

        // 그림자 효과 추가
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2, -2);

        // RectTransform 설정
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = sizeDelta;

        Debug.Log($"Text UI '{name}' added successfully!");
        return text;
    }

    /// <summary>
    /// Canvas에 Button UI를 추가합니다.
    /// </summary>
    public Button AddButtonUI(string name, string buttonText, Vector2 position, Vector2 sizeDelta,
                             Color? buttonColor = null, int fontSize = 20)
    {
        if (canvas == null)
        {
            Debug.LogError("Canvas is not created. Call CreateCanvas() first.");
            return null;
        }

        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(canvas.transform, false);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = buttonColor ?? new Color(0.2f, 0.2f, 0.2f, 1f);

        Button button = buttonObj.AddComponent<Button>();

        // 버튼 텍스트 생성
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = buttonText;
        text.color = Color.white;

        RectTransform textRectTransform = textObj.GetComponent<RectTransform>();
        textRectTransform.anchorMin = Vector2.zero;
        textRectTransform.anchorMax = Vector2.one;
        textRectTransform.offsetMin = Vector2.zero;
        textRectTransform.offsetMax = Vector2.zero;

        // 버튼 RectTransform 설정
        RectTransform buttonRectTransform = buttonObj.GetComponent<RectTransform>();
        buttonRectTransform.anchorMin = Vector2.zero;
        buttonRectTransform.anchorMax = Vector2.zero;
        buttonRectTransform.pivot = Vector2.zero;
        buttonRectTransform.anchoredPosition = position;
        buttonRectTransform.sizeDelta = sizeDelta;

        Debug.Log($"Button UI '{name}' added successfully!");
        return button;
    }

    /// <summary>
    /// Canvas에 Image UI를 추가합니다.
    /// </summary>
    public Image AddImageUI(string name, Vector2 position, Vector2 sizeDelta, Color? color = null)
    {
        if (canvas == null)
        {
            Debug.LogError("Canvas is not created. Call CreateCanvas() first.");
            return null;
        }

        GameObject imageObj = new GameObject(name);
        imageObj.transform.SetParent(canvas.transform, false);

        Image image = imageObj.AddComponent<Image>();
        image.color = color ?? Color.white;

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = sizeDelta;

        Debug.Log($"Image UI '{name}' added successfully!");
        return image;
    }

    /// <summary>
    /// 타이머 UI를 생성합니다.
    /// </summary>
    public void CreateTimerUI()
    {
        timerText = AddTextUI("TimerText", "Time: 01:00", new Vector2(20, -20), new Vector2(300, 100),
                             40, TextAnchor.UpperLeft, Color.white);
    }
   
    /// <summary>
    /// 타이머를 업데이트합니다.
    /// </summary>
    private void UpdateTimer()
    {
        if (!isTimerRunning || timerText == null)
        {
            return;
        }

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0)
        {
            remainingTime = 0;
            isTimerRunning = false;
            OnTimeOut();
        }

        // 시간:분:초 형식으로 표시
        int minutes = (int)remainingTime / 60;
        int seconds = (int)remainingTime % 60;
        timerText.text = $"Time: {minutes:D2}:{seconds:D2}";

        // 시간이 10초 이하일 때 깜빡임 효과
        if (remainingTime <= 10)
        {
            blinkTimer += Time.deltaTime;

            // blinkInterval 마다 색상 토글
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer -= blinkInterval;
            }

            // 깜빡임 효과: 현재 색상을 토글
            float blinkPhase = blinkTimer / blinkInterval;  // 0 ~ 1 사이의 값
            if (blinkPhase < 0.5f)
            {
                timerText.color = Color.red;  // 빨간색
            }
            else
            {
                timerText.color = Color.white;  // 흰색
            }
        }
        else
        {
            timerText.color = Color.white;
            blinkTimer = 0f;  // 타이머 리셋
        }
    }

    /// <summary>
    /// 시간이 다 되었을 때 호출됩니다.
    /// </summary>
    private void OnTimeOut()
    {
        Debug.Log("시간이 다 되었습니다!");
        // 게임 오버 처리 또는 다음 스테이지로 진행
    }

    /// <summary>
    /// 플레이어가 출구(End 타일)에 도착했는지 확인합니다.
    /// </summary>
    private void CheckExitReached()
    {
        if (hasFoundExit || player == null || dungeon == null)
        {
            return;
        }

        TileMap.Tile endTile = dungeon.End;
        if (endTile == null)
        {
            return;
        }

        // 플레이어의 현재 위치에서 타일 좌표 계산
        Vector3 playerPos = player.transform.position;
        int playerTileX = Mathf.RoundToInt(playerPos.x / Dungeon.TileSize);
        int playerTileY = Mathf.RoundToInt(playerPos.z / Dungeon.TileSize);

        // End 타일 좌표와 비교
        int endTileX = (int)endTile.rect.x;
        int endTileY = (int)endTile.rect.y;

        if (playerTileX == endTileX && playerTileY == endTileY)
        {
            hasFoundExit = true;
            OnExitFound();
        }
    }

    /// <summary>
    /// 출구를 찾았을 때 호출됩니다.
    /// </summary>
    private void OnExitFound()
    {
        Debug.Log("출구를 찾았다!");
        Debug.Log($"End Tile 위치: ({(int)dungeon.End.rect.x}, {(int)dungeon.End.rect.y})");
        dungeon.randomSeed = 0;
        remainingTime = 60f;  // 타이머 리셋
        blinkTimer = 0f;  // 깜빡임 타이머 리셋
        isTimerRunning = true;
        hasFoundExit = false;

        // Main 카메라에서 CameraOcclusion 컴포넌트 획득 및 Clear
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            CameraOcclusion cameraOcclusion = mainCamera.GetComponent<CameraOcclusion>();
            if (cameraOcclusion != null)
            {
                cameraOcclusion.Clear();
                Debug.Log("CameraOcclusion cleared in OnExitFound()");
            }
        }

        dungeon.Generate();
    }

    /// <summary>
    /// Canvas에 접근하기 위한 getter 추가
    /// </summary>
    public Canvas GetCanvas()
    {
        return canvas;
    }
}

