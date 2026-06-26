# CLI 연동 및 채팅 UI 연결 예제 가이드
이 디렉토리는 MCP(Model Context Protocol) 프로토콜을 사용하지 않고도 C# .NET 환경에서 Antigravity CLI(`agy`) 및 기타 대화형 CLI 도구를 실행하고 표준 입출력을 통해 채팅 UI와 연동하는 설계 패턴과 구체적인 예제를 제공합니다.

## 포함된 파일 목록
- [CliConnector.cs](file:///C:/Users/rhqud/repos/law-desktop/Examples/CliConnection/CliConnector.cs)
표준 입출력 리다이렉션, 실시간 스트리밍, 대화형(Interactive) 세션을 처리하는 공통 커넥터 클래스입니다.
- [Examples.cs](file:///C:/Users/rhqud/repos/law-desktop/Examples/CliConnection/Examples.cs)
단순 원샷(One-shot) 호출, 실시간 응답 스트리밍, 타임아웃 및 예외 처리, JSON 기반의 구조화된 데이터 통신 등 다양한 CLI 연동 시나리오를 보여주는 구현 예제 모음입니다.

## CLI 연동 방식 설명
WPF나 Windows Forms 등의 채팅 UI에 CLI를 연결할 때에는 주로 세 가지 방식을 사용합니다.

### 1. 1회성 실행 (One-shot Execution)
질문을 CLI 인자나 표준 입력으로 전달하고 결과 출력을 한 번에 받아서 화면에 바인딩하는 구조입니다.
- 장점: 구현이 단순하며 프로세스 관리가 직관적입니다.
- 단점: 응답이 완료될 때까지 UI에서 대기 시간이 발생합니다.

### 2. 실시간 출력 스트리밍 (Real-time Streaming)
프로세스의 표준 출력 스트림을 실시간으로 관찰(RedirectStandardOutput)하여 한 줄씩 읽은 후 UI 스레드에 디스패치해 채팅창에 점진적으로 띄워주는 구조입니다.
- 장점: 사용자에게 지연 시간이 짧게 느껴지며 반응성이 높습니다.
- 단점: 비동기 데이터 파싱 및 UI 스레드 동기화 처리가 필요합니다.

### 3. 대화형 인터랙티브 세션 (Interactive Session)
프로세스를 백그라운드에 띄워 둔 채 표준 입력(`StandardInput`)과 표준 출력(`StandardOutput`)을 열어두고 대화 세션을 지속적으로 유지하는 방식입니다.
- 장점: 인스턴스를 매번 띄우는 오버헤드가 없으며, 대화 맥락(Context)을 쉽게 유지할 수 있습니다.
- 단점: 프로세스 생명 주기를 수동으로 제어하고 파이프가 닫히지 않도록 안전히 관리해야 합니다.

## 실제 WPF 프로젝트 적용을 위한 코드 조각
WPF UI 스레드와 스트리밍 연동 시 다음과 같이 `Dispatcher`를 활용해 UI를 실시간으로 갱신합니다.
```csharp
var connector = new CliConnector("agy.exe");
await connector.ExecuteStreamAsync(
    new[] { "--print", "질문 내용" },
    onDataReceived: (line) =>
    {
        // WPF UI 스레드로 안전하게 마샬링하여 텍스트 추가
        Application.Current.Dispatcher.Invoke(() =>
        {
            ChatTextBox.Text += line + Environment.NewLine;
        });
    },
    onErrorReceived: (err) =>
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ErrorStatusBar.Text = $"에러 발생: {err}";
        });
    }
);
```
