# Klavye Işık Stüdyosu

Clevo tabanlı **Monster** dizüstülerin **tek bölge RGB** klavye ışığı için hafif, Türkçe bir kontrol programı.
35 efekt (müzik, ekran rengi, yazma tepkisi, doğa efektleri…), düşük işlemci kullanımı, tek dosya, kurulum gerektirmez.

> Resmî bir Monster / Clevo ürünü değildir; bağımsız, açık kaynaklı bir projedir.

![Klavye Işık Stüdyosu](docs/ekran-goruntusu.png)

**[⬇️ İndir (Releases)](../../releases/latest)** · **[Modelini bul](#modelini-bul)** · [Windows engelliyorsa ne yapmalı?](#windows-programı-engelliyorsa)

## Modelini bul

Program açıldığında modeli ve klavye türünü BIOS'tan **kendisi okur** ve üst satırda gösterir (örn. `Monster Abra A5 V16.7 · Tek bölge RGB · Doğrudan sürücü bağlantısı`).

Modeline tıkla; kurulum ve o modele özel bilgiler ayrı sayfada. **Hepsi için indirme dosyası aynı** — program modeli kendisi tanır.

**Monster Abra A5:** [V15](modeller/monster-abra-a5-v15.md) · [V16.6](modeller/monster-abra-a5-v16-6.md) · [V16.7](modeller/monster-abra-a5-v16-7.md) · [V16.8](modeller/monster-abra-a5-v16-8.md) · [V17.2](modeller/monster-abra-a5-v17-2.md) · [V17.4](modeller/monster-abra-a5-v17-4.md) · [V20](modeller/monster-abra-a5-v20.md) · [V21](modeller/monster-abra-a5-v21.md) · [V22](modeller/monster-abra-a5-v22.md)

**Monster Abra A7:** [V12.2](modeller/monster-abra-a7-v12-2.md) · [V12.5](modeller/monster-abra-a7-v12-5.md) · [V13.2](modeller/monster-abra-a7-v13-2.md) · [V15.3](modeller/monster-abra-a7-v15-3.md) · [V16.2](modeller/monster-abra-a7-v16-2.md)

**Monster Tulpar T5:** [V23.2](modeller/monster-tulpar-t5-v23-2.md)

**Monster Tulpar T6:** [V3](modeller/monster-tulpar-t6-v3.md)

| Model | Klavye | Durum |
|---|---|---|
| [Monster Abra A5 V15](modeller/monster-abra-a5-v15.md) | Tek bölge RGB | 🟡 Muhtemelen |
| [Monster Abra A5 V16.6](modeller/monster-abra-a5-v16-6.md) | Tek bölge RGB | 🟢 Uyumlu |
| [Monster Abra A5 V16.7](modeller/monster-abra-a5-v16-7.md) | Tek bölge RGB | ✅ Test edildi |
| [Monster Abra A5 V16.8](modeller/monster-abra-a5-v16-8.md) | Tek bölge RGB | 🟢 Uyumlu |
| [Monster Abra A5 V17.2](modeller/monster-abra-a5-v17-2.md) | Tek bölge RGB | 🟢 Uyumlu |
| [Monster Abra A5 V17.4](modeller/monster-abra-a5-v17-4.md) | Tek bölge RGB | 🟢 Uyumlu |
| [Monster Abra A5 V20](modeller/monster-abra-a5-v20.md) | Tek bölge RGB | 🟡 Muhtemelen |
| [Monster Abra A5 V21](modeller/monster-abra-a5-v21.md) | Tek bölge RGB | 🟡 Muhtemelen |
| [Monster Abra A5 V22](modeller/monster-abra-a5-v22.md) | Tek bölge RGB | 🟡 Muhtemelen |
| [Monster Abra A7 V12.2](modeller/monster-abra-a7-v12-2.md) | Tek bölge RGB | 🟢 Uyumlu |
| [Monster Abra A7 V12.5](modeller/monster-abra-a7-v12-5.md) | Tek bölge RGB | 🟢 Uyumlu |
| [Monster Abra A7 V13.2](modeller/monster-abra-a7-v13-2.md) | Tek bölge RGB | 🟢 Uyumlu |
| [Monster Abra A7 V15.3](modeller/monster-abra-a7-v15-3.md) | Tek bölge RGB | 🟡 Muhtemelen |
| [Monster Abra A7 V16.2](modeller/monster-abra-a7-v16-2.md) | Tek bölge RGB | 🟡 Muhtemelen |
| [Monster Tulpar T5 V23.2](modeller/monster-tulpar-t5-v23-2.md) | Tek bölge RGB | 🟡 Muhtemelen |
| [Monster Tulpar T6 V3](modeller/monster-tulpar-t6-v3.md) | Tek bölge RGB | 🟡 Muhtemelen |

Monster'ın sitesinde "**RGB Tek Bölge**" yazan Clevo tabanlı diğer modellerde de çalışması beklenir. Modelin listede yoksa ya da denediysen **[sonucu bildir](https://github.com/yysalih/klavye-isik-studyosu/issues/new?template=model-raporu.yml)**.

### Desteklenmeyen modeller

| Model | Klavye | Neden |
|---|---|---|
| [Monster Tulpar T7 V20.6](modeller/monster-tulpar-t7-v20-6.md) | Tek bölge RGB | Tongfang kasa (farklı denetleyici) |
| [Monster Tulpar T5 V18 ve V19](modeller/monster-tulpar-t5-v18-ve-v19.md) | RGB | Tongfang kasa (farklı denetleyici) |
| [Monster Tulpar T7 V21.5](modeller/monster-tulpar-t7-v21-5.md) | 4 bölge RGB | 4 bölgeli klavye |
| [Monster Tulpar T7 V26](modeller/monster-tulpar-t7-v26.md) | 4 bölge RGB | 4 bölgeli klavye |
| [Monster Semruk S7 ve S8](modeller/monster-semruk-s7-ve-s8.md) | Tuş başına RGB | Tuş başına RGB |
| [Monster Huma H4, H5, H7](modeller/monster-huma-h4-h5-h7.md) | Tek renk (beyaz) | Tek renk, renk değişmez |
| [Monster Markut M7](modeller/monster-markut-m7.md) | Tek renk (beyaz) | Tek renk, renk değişmez |

3 bölgeli eski Clevo klavyeler deneysel olarak desteklenir (üç bölge aynı renge ayarlanır).

## Windows programı engelliyorsa

Program **imzasız** olduğu için (dijital imza sertifikası ücretli) Windows ilk açılışta onu tanımaz ve engelleyebilir.
Bu bir virüs uyarısı değildir; kaynak kodun tamamı bu sayfada açıktır. Karşına hangi mesaj çıktıysa ona göre:

**1. Mavi ekran: "Windows kişisel bilgisayarınızı korudu"** (en sık görülen)

1. Yazının altındaki **"Ek bilgi"** bağlantısına tıkla.
2. Çıkan **"Yine de çalıştır"** düğmesine bas. Bunu bir kez yapman yeterli.

**2. "Yine de çalıştır" düğmesi yoksa ya da program hiç açılmıyorsa**

Dosyanın "internetten indirildi" işaretini kaldır:

1. Çıkarttığın klasörü sil.
2. İndirdiğin **zip dosyasına** sağ tıkla → **Özellikler**.
3. En alttaki **"Engellemeyi kaldır"** kutusunu işaretle → **Tamam**.
4. Zip'i yeniden çıkart ve `KlavyeIsik.exe`'yi aç.

**3. "Tehdit bulundu" bildirimi geldiyse ya da `KlavyeIsik.exe` kaybolduysa**

Windows Defender dosyayı yanlışlıkla karantinaya almıştır:

1. **Başlat** → **Windows Güvenliği** → **Virüs ve tehdit koruması** → **Koruma geçmişi**.
2. KlavyeIsik ile ilgili kaydı aç → **Eylemler** → **Geri yükle** (ya da **Cihazda izin ver**).
3. Tekrar olmasın diye: **Virüs ve tehdit koruması** → **Ayarları yönet** → **Dışlamalar** →
   **Dışlama ekle veya kaldır** → **Dışlama ekle** → **Klasör** → programın klasörünü seç.

**4. "Akıllı Uygulama Denetimi bu uygulamayı engelledi" yazıyorsa** (Windows 11)

1. Önce 2. adımı ("Engellemeyi kaldır") dene.
2. Yine engellenirse: **Windows Güvenliği** → **Uygulama ve tarayıcı denetimi** → **Akıllı Uygulama Denetimi ayarları** → **Kapalı**.
   ⚠️ Bazı Windows sürümlerinde bu ayar kapatıldıktan sonra Windows yeniden kurulmadan tekrar açılamaz; son çare olarak kullan.

> Defender'ı tamamen kapatma; izni sadece bu programın klasörüne ver.
> Dosyanın sağlamlığından emin olmak istersen zip'i [VirusTotal](https://www.virustotal.com)'a yükleyip taratabilir
> ya da programı bu sayfadaki kaynak koddan kendin derleyebilirsin.

## Özellikler

- **35 efekt**, 5 grupta:
  - **Temel:** Sabit, Nefes, İki Renk, Kalp Atışı, Yanıp Sönme
  - **Renk Şovu:** Gökkuşağı, Renk Akışı, Neon, Gün Batımı, Pastel Rüya, Lavanta Akşamı, Zen Akışı, Disko, Polis Çakarı…
  - **Doğa & Atmosfer:** Okyanus, Kuzey Işıkları, Lav, Mum, Ateş, Fırtına, Matrix, Galaksi
  - **Etkileşimli:** yazınca parlayan efektler, müziğe göre yanan efektler (ses kaydı yapmaz, sadece çıkış seviyesini okur), **ekran rengi** ve **ekranın alt kısmına göre ambiyans**
  - **Sistem:** işlemci yükü, pil durumu, gün döngüsü
- İki renk seçimi, parlaklık, hız; 14 hazır renk
- **Performans modları:** Eko (12 fps) / Dengeli (20 fps) / Akıcı (30 fps); pildeyken otomatik Eko
- Renk değişmediyse klavyeye hiçbir şey gönderilmez; sabit efektlerde program tamamen uyur
- Kilitlenince ışığı kapatma: Hemen / 1 dk / 5 dk / Asla
- Bildirim alanında (saatin yanında) çalışır, Windows açılınca başlayabilir
- Kısayollar: `Ctrl+Alt+K` aç/kapat · `Ctrl+Alt+PgDn/PgUp` efekt · `Ctrl+Alt+Home/End` parlaklık

Tipik kullanım: animasyonlu efektlerde ~%0,1–0,5 işlemci, ~10–15 MB bellek (tepsideyken).

## Kurulum

1. [Releases](../../releases) sayfasından son sürümün zip'ini indir ve **bir klasöre çıkart**.
2. `KlavyeIsik.exe`'yi çalıştır. Kurulum, .NET ya da yönetici izni gerekmez.
3. Bilgisayarda Monster/Clevo **Kontrol Merkezi (Control Center)** sürücüsü kurulu olmalı
   (Fn tuşlarıyla klavye ışığı değişiyorsa genelde kuruludur).

Windows programı engellerse: [Windows programı engelliyorsa](#windows-programı-engelliyorsa) bölümüne bak.

**Eski modeller (Control Center 2.x):** Bu modellerde klavyeye yalnızca yönetici izniyle erişilebilir.
Program bunu algılar; üst satırdaki yazıya tıklayınca yönetici olarak yeniden başlar.

## Nasıl çalışır?

Clevo kasalarında klavye ışığı BIOS'taki tek bir komutla (`0x67`) ayarlanır. Program üç yolu sırayla dener:

| Yol | Ne zaman | Yönetici |
|---|---|---|
| Insyde DCHU sürücüsü (`ACPI\CLV0001`, `_DSM`, IOCTL `0x322400`) | Control Center 3.x ve sonrası (~2019+) | Gerekmez |
| `InsydeDCHU.dll` → `SetDCHU_Data(0x67, …)` | Aynı sürücü, yedek yol | Gerekmez |
| WMI `root\WMI` → `CLEVO_GET.SetKBLED` | Control Center 2.x (~2015–2019) | Gerekir |

- Renk biçimi: `0xF0000000 | B<<16 | R<<8 | G` (3 bölgede `0xF1…`, `0xF2…` de gönderilir).
- Klavye türü: `0x0D` komutunun döndürdüğü tamponun 15. baytı (`0x06` tek bölge, `0x02` 3 bölge, `0x01` beyaz, `0xF3` tuş başına);
  eski makinelerde `0x52` özellik listesi.
- Bu makinelerde donanımın kendi animasyonları çalışmadığı için tüm efektler yazılımla, sadece renk değiştiğinde komut gönderilerek yapılır.

Komut numaraları ve tür kodları için [tuxedo-drivers](https://github.com/tuxedocomputers/tuxedo-drivers)
(`clevo_leds.h`, `clevo_acpi.c`, `clevo_wmi.c`) kaynak kodundan yararlanıldı.

## Gizlilik

- Ses kaydı yapmaz; yalnızca hoparlör çıkışının anlık seviyesini okur.
- Basılan tuşları kaydetmez; yalnızca "bir tuşa basıldı" bilgisini kullanır.
- Ekran görüntüsü saklamaz; ekranı 64×36 piksele küçültüp ortalama rengi hesaplar.
- İnternete bağlanmaz. Ayarlar `%APPDATA%\KlavyeIsik\ayarlar.json` dosyasında durur.

## Kaynaktan derleme

Gereken: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

## Destekçiler

Projeye katkı ve destek verenler:

<table>
  <tr>
    <td align="center"><a href="https://github.com/yysalih"><img src="https://github.com/yysalih.png?size=100" width="80" alt="yysalih"/><br/><b>yysalih</b></a><br/>Geliştirici</td>
    <td align="center"><a href="https://github.com/Muhammed-Yakut"><img src="https://github.com/Muhammed-Yakut.png?size=100" width="80" alt="Muhammed-Yakut"/><br/><b>Muhammed-Yakut</b></a><br/>Destekçi</td>
  </tr>
</table>

Sen de kendi modelinde deneyip [sonucu bildirerek](../../issues/new?template=model-raporu.yml) destek olabilirsin.

## Lisans

[MIT](LICENSE)

---

### English

Lightweight Windows controller for the **single-zone RGB keyboard backlight** of Clevo-based laptops
(Monster Abra / Tulpar and similar rebrands). 35 software effects (audio-reactive, screen-ambient, typing-reactive…),
very low CPU use, no install, no admin rights on modern models. It talks to the Insyde DCHU driver directly
(command `0x67`), falls back to `InsydeDCHU.dll` and to the legacy `CLEVO_GET` WMI interface, and detects the
keyboard type (single-zone / 3-zone / white / per-key) from the BIOS. Not affiliated with Monster or Clevo. UI is in Turkish.
