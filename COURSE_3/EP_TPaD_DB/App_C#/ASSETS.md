# Источники изображений

Изображения для учебного C#-приложения. Названия и эмблемы принадлежат их правообладателям.
Цвета и пропорции эмблем сохранены; растры переведены в PNG без потерь.
Все изображения встроены в сборку как EmbeddedResource.

| Файл в App/Assets | Источник | Исходный размер |
|---|---|---|
| Salavat.png | [ХК «Салават Юлаев»](https://hcsalavat.ru/upload/resize_cache/webp/iblock/07c/vrk4thzze93p58w33ky9vm7ag77q6xmh.webp) | 200 × 200 |
| Tolpar.png | [ХК «Салават Юлаев»: «Толпар»](https://hcsalavat.ru/upload/iblock/472/5mnej9gjcxe9bz20fik3yumvwy73j1k1.png) | 200 × 200 |
| Agidel.png | [ХК «Салават Юлаев»: «Агидель»](https://hcsalavat.ru/upload/iblock/4c1/miwwv9b8yqb5ii7hclw541b17siwczws.png) | 200 × 200 |
| Astra.png | [ФХР, АНО ДОСШ «Астра»](https://www.filial.fhr.ru/schools/ano-dossh-astra-7471714/), [изображение](https://img.fhr.ru/schools/00/8c/7471714.webp) | 978 × 978 |
| Azamatov.png | [Ассоциация МКЦ УЗС, школа им. М. М. Азаматова](https://uralhockey.ru/sport/club/38), [изображение](https://uralhockey.ru/sport/images/clubs/38_4e4b754649e8d0da307b1ea56bfae383.jpg) | 150 × 150 |
| Cska.png | [Официальный сайт ЦСКА](https://cska-hockey.ru/local/templates/cska/img/logo.svg) | SVG; PNG 1020 × 1024 |
| Ska.png | [КХЛ](https://www.khl.ru/images/teams/ru/1436/24) | 200 × 200 |
| Spartak.png | [КХЛ](https://www.khl.ru/images/teams/ru/1436/7) | 200 × 200 |
| Avangard.png | [КХЛ](https://www.khl.ru/images/teams/ru/1436/34) | 200 × 200 |
| AkBars.png | [ХК «Салават Юлаев»: эмблема «Ак Барса»](https://hcsalavat.ru/upload/iblock/917/z1adg8vng70cpa6qr5q5sieojw5lzin2.png) | 200 × 200 |
| HockeyStick.png | [PNGimg.com — Hockey Stick glossy, ID 26544](https://pngimg.com/image/26544), [PNG](https://pngimg.com/uploads/hockey/hockey_PNG102.png) | 1110 × 1110 |
| PuckSurface.png | Встроенный ImageGen, материал использованной хоккейной шайбы; полный запрос в PuckSurface.prompt.txt | 1254 × 1254 |
| IceFrame.png | Архив прежнего фона ImageGen; не включён в сборку и не используется | 1672 × 941 |

Фотография клюшки: **PNGimg.com**, [CC BY-NC 4.0](https://pngimg.com/license).
В приложении использованы две зеркальные копии исходного PNG с прозрачностью.
Исходный SVG ЦСКА сохранён в `App/Assets/Originals/Cska.svg`.

Некоторые эмблемы доступны только в небольшом растровом размере.
Их увеличение не восстанавливает отсутствующие детали и не меняет подлинную эмблему.

Текущий фон создаётся кодом NativeIceDrawing.cs в разрешении окна.
Хоккейная майка, её ткань, швы и полосы рисуются JerseyDrawing.cs.
Малые сколы кромки шайб строятся геометрией; фотографический материал покрывает всю рабочую поверхность.
