# CLAUDE.md — Klavye Işık Stüdyosu

Clevo tabanlı Monster dizüstülerin (Abra, Tulpar…) **tek bölge RGB** klavye ışığı için Windows programı.
C# / .NET 8 WinForms, tek exe. Depo sahibi: `yysalih`. Kullanıcı Türkçe konuşur; arayüz, README ve commit mesajları Türkçe.

## Kurallar

- **GitHub'a yayın (push, release, depo ayarı) kullanıcı açıkça istemeden yapılmaz.**
- Commit kimliği: `yysalih <104041213+yysalih@users.noreply.github.com>` (depo yerel ayarı). Bilgisayarın genel git
  ayarındaki e-posta başka birine ait; kullanma, kişisel e-posta paylaşma.
- Kullanıcının çalışan programını bozma: yerel kurulum `..\Program\KlavyeIsik.exe` (framework-dependent). Yeniden
  yayınlamadan önce `Get-Process KlavyeIsik | Stop-Process -Force` (exe kilitli olur), sonra `--tray` ile yeniden başlat.
- Destekçiler: `Muhammed-Yakut` (README "Destekçiler" bölümünde).

## Dosyalar

| Dosya | İçerik |
|---|---|
| `ClevoKeyboard.cs` | Donanım katmanı: bağlantı yolları, klavye türü algılama, `SetColor` |
| `LightEngine.cs` | Efekt motoru (ayrı iş parçacığı, sadece renk değişince gönderir, fps/Eko, duraklatma) |
| `Effects.cs` | 35 efekt (`EffectCatalog.All`), kategoriler, `Needs` bayrakları |
| `Sensors.cs` | Ses ölçer (WASAPI), ekran örnekleyici (GDI 64×36), CPU ölçer, klavye kancası (sadece "basıldı") |
| `MainForm.cs` | Arayüz, tepsi, kısayollar, kilit/ekran/uyku olayları, "Kilitte ışığı kapat" zamanlayıcısı |
| `Controls.cs` | Koyu temalı özel kontroller, klavye önizlemesi |
| `AppSettings.cs` | `%APPDATA%\KlavyeIsik\ayarlar.json`, otomatik başlatma (HKCU Run ya da yönetici için Görev Zamanlayıcı) |
| `Program.cs` | Tek örnek (mutex), `--tray`, `--elevated`, `--snapshot <png>` |
| `modeller/*.md` | Her model için sayfa — **elle düzenleme**, `araclar/model_sayfalari.py` üretir |
| `araclar/model_sayfalari.py` | Model listesi (M) → `modeller/` + README "Modelini bul" bölümü |
| `araclar/paketle.ps1` | Yayın zip'i: `dist\KlavyeIsik-v<sürüm>-win-x64.zip` (sürüm `KlavyeIsik.csproj` içinde) |
| `araclar/OKU_BENI.txt` | Zip'e konan kurulum ve "Windows engellerse" rehberi |
| `eski/color_control.bat` | Projenin çıkış noktası olan eski menü betiği (üçüncü parti CLEVO_KeyboardColour aracını çağırır; sadece arşiv) |

## Donanım protokolü (doğrulandı: Monster Abra A5 V16.7 = Clevo NH5x/NH7x, model 0x5015)

- Aygıt arayüzü GUID `86994c74-ad43-4812-b7e7-0c420b5c5fd7` → `CM_Get_Device_Interface_ListW` → `CreateFileW` →
  `DeviceIoControl(0x322400)`, 1056 baytlık giriş: `_DSM` GUID `93f224e4-fbdc-4bbf-add6-db71bdc0afad` (0..15),
  `"_DSM"` (20), komut (24), `04 01 02 00 00 01` (28..33), 4 baytlık argüman (34). Yönetici gerekmez.
- Çıkış: `"BoeA"` imzası, uzunluk, sayı; ilk argüman tür(12: 0=tamsayı, 2=tampon), uzunluk(14), veri(16…).
- Komutlar (her yolda aynı numara): `0x67` renk = `bölge | B<<16 | R<<8 | G` (bölge `0xF0` tek/sol, `0xF1`, `0xF2`);
  `0x0D` bilgi tamponu, `[0x0F]` klavye türü: `0x06` tek bölge, `0x02` 3 bölge, `0x01` beyaz, `0xF3` tuş başına
  (bu makinede `0x06` okundu); `0x52` özellik listesi (eski makinelerde yedek tür algılama).
- Gönderim başına ~3,9 ms CPU (çekirdek tarafı) → sadece görünür değişiklikte gönder.
- **Donanım animasyon modları bu makinede çalışmıyor**; tüm efektler yazılımla.
- Yedek yollar: `InsydeDCHU.dll` `SetDCHU_Data(0x67,&arg,4)` (DriverStore `insydedchu*`), eski WMI
  `root\WMI CLEVO_GET.SetKBLED` (Control Center 2.x, yönetici ister, fps 10 ile sınırlı).
- Tongfang/Uniwill kasalı Monster'lar (Tulpar T7 V20.6, bazı T5 V18–19 / Abra V11–15) **desteklenmez**; EC'ye
  körlemesine yazmak riskli, donanım olmadan deneme.
- Kaynak: tuxedo-drivers (`clevo_leds.h`, `clevo_acpi.c`, `clevo_wmi.c`).

## Derleme / test (Windows gerekir)

```powershell
# kullanıcının kendi kopyasını güncelle
Get-Process KlavyeIsik -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ..\Program
Start-Process ..\Program\KlavyeIsik.exe -ArgumentList --tray

# arayüz ekran görüntüsü (çalışan örneği önce kapat; mutex yüzünden)
..\Program\KlavyeIsik.exe --snapshot C:\temp\ekran.png
```

- Performans hedefi: animasyonlu efektte ≲%0,5 CPU, tepside ~10–15 MB RAM.
- Bir değişiklikten sonra: derle, `--snapshot` ile arayüze bak, CPU'yu ölç, kullanıcının programını yeniden başlat.

## Yayın süreci (kullanıcı onayıyla)

1. `KlavyeIsik.csproj` içinde `<Version>` artır.
2. `powershell -ExecutionPolicy Bypass -File araclar\paketle.ps1`
3. Commit + push, sonra `gh release create v<sürüm> dist\KlavyeIsik-v<sürüm>-win-x64.zip --repo yysalih/klavye-isik-studyosu`
   (notlarda: kurulum, "Windows engellerse" adımları, model listesi bağlantıları).
4. Model durumu değiştiyse (ör. bir kullanıcı raporu geldi): `araclar/model_sayfalari.py` içindeki `M` listesini güncelle,
   betiği çalıştır, commit et.
