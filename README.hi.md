<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.md">English</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/build-governor/readme.png" alt="Build Governor" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/build-governor/actions/workflows/publish.yml"><img src="https://github.com/mcp-tool-shop-org/build-governor/actions/workflows/publish.yml/badge.svg" alt="CI"></a>
  <a href="https://www.nuget.org/packages/Gov.Protocol"><img src="https://img.shields.io/nuget/v/Gov.Protocol" alt="NuGet Gov.Protocol"></a>
  <a href="https://www.nuget.org/packages/Gov.Common"><img src="https://img.shields.io/nuget/v/Gov.Common" alt="NuGet Gov.Common"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/build-governor/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**C++ बिल्ड के दौरान मेमोरी की कमी से स्वचालित सुरक्षा। किसी भी मैनुअल चरण की आवश्यकता नहीं है।**

## क्यों

समानांतर C++ बिल्ड (`cmake --parallel`, `msbuild /m`, `ninja -j`) आसानी से सिस्टम मेमोरी को समाप्त कर सकते हैं:

- प्रत्येक `cl.exe` इंस्टेंस 1-4 जीबी रैम का उपयोग कर सकता है (टेम्प्लेट, एलटीसीजी, भारी हेडर)
- बिल्ड सिस्टम एन समानांतर जॉब शुरू करते हैं और सर्वोत्तम परिणाम की उम्मीद करते हैं।
- जब रैम समाप्त हो जाती है: सिस्टम जम जाता है, या `CL.exe कोड 1 के साथ समाप्त हो गया` (कोई निदान नहीं)।
- महत्वपूर्ण माप **कमिट चार्ज** है, न कि "फ्री रैम"।


बिल्ड गवर्नर एक हल्का गवर्नर है जो **स्वचालित रूप से** आपके बिल्ड सिस्टम और कंपाइलर के बीच स्थित होता है:

1. **शून्य-कॉन्फ़िगरेशन सुरक्षा** — रैपर पहले बिल्ड पर स्वचालित रूप से गवर्नर को शुरू करते हैं।
2. **अनुकूल समानांतरता** जो कमिट चार्ज पर आधारित है, न कि जॉब की संख्या पर।
3. **शांत विफलता → कार्रवाई योग्य निदान** — "मेमोरी प्रेशर का पता चला, -j4 की सिफारिश की जाती है"।
4. **स्वचालित थ्रॉटलिंग** — बिल्ड धीमे हो जाते हैं, क्रैश नहीं होते।
5. **सुरक्षा उपाय** — यदि गवर्नर बंद है, तो उपकरण बिना नियंत्रण के चलते हैं।

## त्वरित शुरुआत (स्वचालित सुरक्षा)

```powershell
# One-time setup (no admin required)
cd build-governor
.\scripts\enable-autostart.ps1

# That's it! All builds are now protected
cmake --build . --parallel 16
msbuild /m:16
ninja -j 8
```

रैपर स्वचालित रूप से:
- यदि यह चल नहीं रहा है तो गवर्नर को शुरू करें।
- मेमोरी की निगरानी करें और आवश्यकता पड़ने पर थ्रॉटल करें।
- 30 मिनट की निष्क्रियता के बाद बंद हो जाएं।

## वैकल्पिक: विंडोज सर्विस (एंटरप्राइज)

सभी उपयोगकर्ताओं के लिए हमेशा-ऑन सुरक्षा के लिए:

```powershell
# Requires Administrator
.\scripts\install-service.ps1
```

## मैनुअल मोड

यदि आप स्पष्ट नियंत्रण पसंद करते हैं:

```powershell
# 1. Build
dotnet build -c Release
dotnet publish src/Gov.Wrapper.CL -c Release -o bin/wrappers
dotnet publish src/Gov.Wrapper.Link -c Release -o bin/wrappers
dotnet publish src/Gov.Cli -c Release -o bin/cli

# 2. Start governor (in one terminal)
dotnet run --project src/Gov.Service -c Release

# 3. Run your build (in another terminal)
bin/cli/gov.exe run -- cmake --build . --parallel 16
```

## NuGet पैकेज

| पैकेज | संस्करण | विवरण |
|---------|---------|-------------|
| [`Gov.Protocol`](https://www.nuget.org/packages/Gov.Protocol) | [![NuGet](https://img.shields.io/nuget/v/Gov.Protocol)](https://www.nuget.org/packages/Gov.Protocol) | क्लाइंट-सर्विस संचार के लिए साझा संदेश DTOs, जो नामित पाइपों के माध्यम से होते हैं। |
| [`Gov.Common`](https://www.nuget.org/packages/Gov.Common) | [![NuGet](https://img.shields.io/nuget/v/Gov.Common)](https://www.nuget.org/packages/Gov.Common) | विंडोज मेमोरी मेट्रिक्स, OOM वर्गीकरण, ऑटो-स्टार्ट क्लाइंट। |

```xml
<!-- Gov.Protocol — message DTOs only (no Windows dependency) -->
<PackageReference Include="Gov.Protocol" Version="1.*" />

<!-- Gov.Common — memory metrics + OOM classifier (Windows) -->
<PackageReference Include="Gov.Common" Version="1.*" />
```

## यह कैसे काम करता है

### स्वचालित सुरक्षा प्रवाह

```
  cmake --build .
        │
        ▼
    ┌───────────┐
    │  cl.exe   │ ← Actually the wrapper (in PATH)
    │  wrapper  │
    └─────┬─────┘
          │
          ▼
  ┌───────────────────┐
  │ Governor running? │
  └─────────┬─────────┘
       No   │   Yes
            │
     ┌──────┴──────┐
     ▼             ▼
  Auto-start    Connect
  Governor      directly
     │             │
     └──────┬──────┘
            ▼
    Request tokens
            │
            ▼
    Run real cl.exe
            │
            ▼
    Release tokens
```

### आर्किटेक्चर

```
                    ┌─────────────────┐
                    │  Gov.Service    │
                    │  (Token Pool)   │
                    │  - Monitor RAM  │
                    │  - Grant tokens │
                    │  - Classify OOM │
                    └────────┬────────┘
                             │ Named Pipe
         ┌───────────────────┼───────────────────┐
         │                   │                   │
    ┌────┴────┐        ┌────┴────┐        ┌────┴────┐
    │ cl.exe  │        │ cl.exe  │        │link.exe │
    │ wrapper │        │ wrapper │        │ wrapper │
    └────┬────┘        └────┬────┘        └────┬────┘
         │                   │                   │
    ┌────┴────┐        ┌────┴────┐        ┌────┴────┐
    │ real    │        │ real    │        │ real    │
    │ cl.exe  │        │ cl.exe  │        │ link.exe│
    └─────────┘        └─────────┘        └─────────┘
```

## टोकन कॉस्ट मॉडल

| क्रिया | टोकन | टिप्पणियाँ |
|--------|--------|-------|
| सामान्य संकलन | 1 | बेसलाइन |
| भारी संकलन (बूस्ट/gRPC) | 2–4 | टेम्प्लेट-भारी |
| `/GL` के साथ संकलन | +3 | LTCG कोड जनरेशन |
| लिंक | 4 | बेस लिंक कॉस्ट |
| `/LTCG` के साथ लिंक | 8–12 | पूर्ण LTCG |

## थ्रॉटल स्तर

| कमिट अनुपात | स्तर | व्यवहार |
|--------------|-------|----------|
| < 80% | सामान्य | तुरंत टोकन प्रदान करें |
| 80–88% | सावधानी | धीमी गति से टोकन प्रदान करें, 200 मिलीसेकंड का विलंब |
| 88–92% | सॉफ्टस्टॉप | महत्वपूर्ण विलंब, 500 मिलीसेकंड |
| > 92% | हार्डस्टॉप | नए टोकन अस्वीकार करें |

## विफलता वर्गीकरण

जब कोई बिल्ड टूल त्रुटि के साथ समाप्त होता है, तो गवर्नर इसे वर्गीकृत करता है:

- **LikelyOOM**: उच्च कमिट अनुपात + प्रक्रिया ने उच्च स्तर पर पीक बनाया + कंपाइलर निदान अनुपस्थित
- **LikelyPagingDeath**: मध्यम दबाव संकेत
- **NormalCompileError**: कंपाइलर निदान stderr में मौजूद
- **Unknown**: निर्धारित नहीं कर सकते

OOM होने पर, आपको दिखाई देगा:
```
╔══════════════════════════════════════════════════════════════════╗
║  BUILD FAILED: Memory Pressure Detected                          ║
╠══════════════════════════════════════════════════════════════════╣
║  Exit code: 1                                                    ║
║  System commit: 94% (45.2 GB / 48.0 GB)                          ║
║  Process peak:  3.1 GB                                           ║
╠══════════════════════════════════════════════════════════════════╣
║  Recommendation: Reduce parallelism                              ║
║    CMAKE_BUILD_PARALLEL_LEVEL=4                                  ║
║    MSBuild: /m:4                                                 ║
║    Ninja: -j4                                                    ║
╚══════════════════════════════════════════════════════════════════╝
```

## सुरक्षा विशेषताएं

- **सुरक्षा उपाय**: यदि गवर्नर अनुपलब्ध है, तो रैपर बिना नियंत्रण के टूल चलाते हैं।
- **लीज TTL**: यदि रैपर क्रैश होता है, तो टोकन 30 मिनट के बाद स्वचालित रूप से पुनः प्राप्त हो जाते हैं।
- **कोई डेडलॉक नहीं**: सभी पाइप ऑपरेशनों पर टाइमआउट।
- **टूल ऑटो-डिटेक्शन**: वास्तविक `cl.exe/link.exe` खोजने के लिए `vswhere` का उपयोग करता है।

## CLI कमांड

```powershell
# Run a governed build
gov run -- cmake --build . --parallel 16

# Check governor status
gov status

# Run without auto-starting governor
gov run --no-start -- ninja -j 8
```

## पर्यावरण चर

| चर | विवरण |
|----------|-------------|
| `GOV_REAL_CL` | वास्तविक `cl.exe` का पथ (vswhere के माध्यम से स्वचालित रूप से पता लगाया गया) |
| `GOV_REAL_LINK` | वास्तविक `link.exe` का पथ (स्वचालित रूप से पता लगाया गया) |
| `GOV_ENABLED` | `gov run` द्वारा सेट किया गया, जो नियंत्रित मोड को इंगित करता है। |
| `GOV_SERVICE_PATH` | ऑटो-स्टार्ट के लिए `Gov.Service.exe` का पथ। |
| `GOV_DEBUG` | विस्तृत ऑटो-स्टार्ट लॉगिंग के लिए "1" पर सेट करें। |

## परियोजना संरचना

```
build-governor/
├── src/
│   ├── Gov.Protocol/    # Shared DTOs
│   ├── Gov.Common/      # Windows metrics, classifier, auto-start
│   ├── Gov.Service/     # Background governor (supports --background)
│   ├── Gov.Wrapper.CL/  # cl.exe shim (auto-starts governor)
│   ├── Gov.Wrapper.Link/# link.exe shim
│   └── Gov.Cli/         # `gov` command
├── scripts/
│   ├── enable-autostart.ps1  # User setup (no admin)
│   ├── install-service.ps1   # Windows Service (admin)
│   └── uninstall-service.ps1 # Remove service
├── bin/
│   ├── wrappers/        # Published shims
│   ├── service/         # Published service
│   └── cli/             # Published CLI
└── gov-env.cmd          # Manual PATH setup
```

## ऑटो-स्टार्ट व्यवहार

रैपर एक वैश्विक म्यूटेक्स का उपयोग करते हैं ताकि यह सुनिश्चित किया जा सके कि केवल एक ही गवर्नर इंस्टेंस चल रहा है।
जब कई कंपाइलर एक साथ शुरू होते हैं:

1. पहला रैपर म्यूटेक्स प्राप्त करता है, यह जांचता है कि क्या गवर्नर चल रहा है।
2. यदि नहीं, तो `Gov.Service.exe --background` शुरू करता है।
3. अन्य रैपर म्यूटेक्स पर प्रतीक्षा करते हैं, फिर अब चल रहे गवर्नर से जुड़ते हैं।
4. पृष्ठभूमि मोड: गवर्नर 30 मिनट की निष्क्रियता के बाद बंद हो जाता है।

## सुरक्षा और डेटा का दायरा

बिल्ड गवर्नर विंडोज पर **पूरी तरह से स्थानीय रूप से** काम करता है — कोई नेटवर्क अनुरोध नहीं, कोई टेलीमेट्री नहीं।

- **पहुंचे जाने वाले डेटा:** यह विंडोज एपीआई के माध्यम से सिस्टम कमिट चार्ज और प्रति-प्रक्रिया मेमोरी की निगरानी करता है। यह नामित पाइपों के माध्यम से बिल्ड टूल के साथ संचार करता है (केवल स्थानीय इंटर-प्रोसेस कम्युनिकेशन)। गवर्नर सेवा 30 मिनट की निष्क्रियता के बाद स्वचालित रूप से बंद हो जाती है।
- **पहुंचे जाने वाले डेटा नहीं:** कोई नेटवर्क अनुरोध नहीं। कोई टेलीमेट्री नहीं। कोई क्रेडेंशियल स्टोरेज नहीं। कोई बिल्ड आर्टिफैक्ट निरीक्षण नहीं — गवर्नर प्रक्रिया समवर्तीता को नियंत्रित करता है, यह स्रोत कोड या बाइनरी को नहीं पढ़ता है।
- **आवश्यक अनुमतियाँ:** CLI और रैपर के लिए मानक उपयोगकर्ता। केवल विंडोज सर्विस इंस्टॉलेशन के लिए व्यवस्थापक।

भेद्यता रिपोर्टिंग के लिए [SECURITY.md](SECURITY.md) देखें।

---

## स्कोरकार्ड

| श्रेणी | स्कोर |
|----------|-------|
| सुरक्षा | 10/10 |
| त्रुटि प्रबंधन | 10/10 |
| ऑपरेटर दस्तावेज़ | 10/10 |
| शिपिंग स्वच्छता | 10/10 |
| पहचान | 10/10 |
| **Overall** | **50/50** |

---

## लाइसेंस

[MIT](LICENSE)

---

<a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a> द्वारा निर्मित।
