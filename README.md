# LawDesktop
로컬 `agy` CLI와 법제처 법령 MCP(Model Context Protocol) 서버를 연동한 C# .NET 8.0 WPF 기반의 오프라인 법률 상담 데스크톱 애플리케이션입니다.

## 🌟 주요 특징
- **API 비용 Zero**: 별도의 OpenAI/Gemini 클라우드 API 키가 필요 없으며, 로컬 PC에 세팅된 `agy cli`를 추론 백엔드로 활용합니다.
- **실시간 법령 및 판례 수집 (RAG)**: 법제처 MCP 서버를 직접 연동하여 사용자의 질문과 관련된 법조문 및 판례 데이터를 실시간으로 가져옵니다.
- **철저한 출처 기반 검증 (Verify Citations)**: AI가 답변을 작성한 후, 실제로 존재하는 법령/판례인지 교차 검증하며, 환각(Hallucination)이 감지된 경우 사용자에게 경고(검증실패 마킹)를 제공합니다.
- **Modern Dark UI**: 눈이 편안한 딥 다크 모드 기반의 채팅방 환경을 선사합니다. 대화창 속 법령/판례 인용 링크를 클릭하면 우측 패널에서 상세 원문을 즉시 읽을 수 있습니다.

## 🛠️ 실행 요구사항
1. **.NET 8.0 SDK 이상** 설치 필요
2. **Antigravity CLI (`agy`)** 설치 및 로그인 세팅 완료 필요
   - CLI가 실행 가능하며 환경 변수 PATH에 등록되어 있어야 합니다.
3. 법제처 MCP를 정상적으로 이용하기 위해 **OC Key**(법제처 오픈 API 연동 키)를 입력하시거나, 기본 데모 키를 그대로 사용할 수 있습니다.

## 🚀 빠른 시작
```bash
# 레포지토리 복제
git clone https://github.com/ByeonggeonGo/law-desktop.git
cd law-desktop

# 빌드 및 실행
dotnet run
```

## 📂 아키텍처 흐름
1. **Intro (의도 분류)**: 질문이 단순 인사인지 검색이 필요한 질문인지 C# 앱 내부에서 판별합니다.
2. **Keyword Extract**: 로컬 `agy --print`를 사용하여 질문에 맞는 정밀 검색용 키워드를 자동으로 추출합니다.
3. **Mcp Search**: 추출된 키워드로 법령 MCP의 `search_law` 및 `search_decisions` API를 직접 호출하여 법조문과 판례를 수집합니다.
4. **Draft Generate (LLM)**: 수집된 원문 텍스트(Context)와 질문을 결합하여 로컬 `agy`에 전달하고, Gemini가 신뢰할 수 있는 초안 답변을 작성합니다.
5. **Outro (인용 검증)**: 법령 MCP의 `verify_citations` API를 호출해 인용구의 진위 여부를 최종 검증하고 환각이 감지된 문구에는 `[법령: 민법 제750조 → 검증실패]` 형태로 치환 마킹하여 UI에 표시합니다.
