# Her Monster modeli için ayrı bir sayfa (modeller/*.md) üretir ve README'deki "Modelini bul" bölümünü günceller.
# Kullanım (depo kökünden):  python araclar/model_sayfalari.py
# Yeni model eklemek / durumunu değiştirmek için aşağıdaki M listesini düzenle ve betiği yeniden çalıştır.
import os, re, sys

REPO = "https://github.com/yysalih/klavye-isik-studyosu"
DL = f"{REPO}/releases/latest"
root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
out_dir = os.path.join(root, "modeller")
os.makedirs(out_dir, exist_ok=True)

TESTED, LIKELY, MAYBE, NO = "tested", "likely", "maybe", "no"
STATUS = {
    TESTED: "✅ Bu modelde test edildi, çalışıyor",
    LIKELY: "🟢 Uyumlu olması bekleniyor (aynı Clevo kasa ailesi)",
    MAYBE: "🟡 Muhtemelen uyumlu (kasa kodu doğrulanmadı)",
    NO: "❌ Desteklenmiyor",
}
SHORT = {TESTED: "✅ Test edildi", LIKELY: "🟢 Uyumlu", MAYBE: "🟡 Muhtemelen", NO: "❌ Desteklenmiyor"}

# (ad, seri, kasa, klavye, durum, dönem/donanım, not)
M = [
    ("Abra A5 V15", "Abra A5", "Clevo ya da Tongfang GK5CP", "Tek bölge RGB", MAYBE, "2020 · i7-10750H · GTX 1650 Ti",
     "Bu seride hem Clevo hem Tongfang kasalı üretimler var. Programı açınca üst satırda \"Tek bölge RGB\" yazıyorsa çalışır; \"Tongfang\" yazıyorsa desteklenmiyor."),
    ("Abra A5 V16.6", "Abra A5", "Clevo NH58DEQ", "Tek bölge RGB", LIKELY, "2021 · 11. nesil Intel",
     "V16.7 ile aynı kasa ailesi."),
    ("Abra A5 V16.7", "Abra A5", "Clevo NH5x / NH7x", "Tek bölge RGB", TESTED, "2021–22 · i5-11400H · GTX 1650",
     "Program bu modelde geliştirildi. Klavye türü BIOS'tan \"tek bölge RGB\" (0x06) olarak okunuyor; tüm efektler, müzik ve ekran modları çalışıyor."),
    ("Abra A5 V16.8", "Abra A5", "Clevo NH5x", "Tek bölge RGB", LIKELY, "i7-11800H",
     "V16.7 ile aynı kasa ailesi ve BIOS serisi."),
    ("Abra A5 V17.2", "Abra A5", "Clevo NH58HKQ", "Tek bölge RGB", LIKELY, "i5-11400H · RTX 3050 Ti", ""),
    ("Abra A5 V17.4", "Abra A5", "Clevo NH58HPQ", "Tek bölge RGB", LIKELY, "11./12. nesil Intel", ""),
    ("Abra A5 V20", "Abra A5", "Clevo", "Tek bölge RGB", MAYBE, "2023+ · i5-12450H – i7-13620H · RTX 3050–4050",
     "V20.x alt sürümlerinin hepsi (V20.1, V20.7…) için geçerli."),
    ("Abra A5 V21", "Abra A5", "Clevo", "Tek bölge RGB", MAYBE, "2024+ · RTX 4050–5050", "V21.x alt sürümlerinin hepsi için geçerli."),
    ("Abra A5 V22", "Abra A5", "Clevo", "Tek bölge RGB", MAYBE, "2025+ · RTX 5050", "V22.x alt sürümlerinin hepsi için geçerli."),
    ("Abra A7 V12.2", "Abra A7", "Clevo NH79DEQ", "Tek bölge RGB", LIKELY, "Tiger Lake (11. nesil Intel)", ""),
    ("Abra A7 V12.5", "Abra A7", "Clevo NH7x", "Tek bölge RGB", LIKELY, "i5-11400H · GTX 1650", ""),
    ("Abra A7 V13.2", "Abra A7", "Clevo NH77DCQ", "Tek bölge RGB", LIKELY, "11. nesil Intel", ""),
    ("Abra A7 V15.3", "Abra A7", "Clevo", "Tek bölge RGB", MAYBE, "2023–24 · i5-13500H · RTX 3050 6 GB · 17,3\"", ""),
    ("Abra A7 V16.2", "Abra A7", "Clevo", "Tek bölge RGB", MAYBE, "2025–26 · i7-13700HX · RTX 5050", "Kontrol Merkezi v7.075 ile geliyor."),
    ("Tulpar T5 V23.2", "Tulpar T5", "Clevo (muhtemel)", "Tek bölge RGB", MAYBE, "i7-12650H · RTX 4060", ""),
    ("Tulpar T6 V3", "Tulpar T6", "Clevo (muhtemel)", "Tek bölge RGB", MAYBE, "2024–26 · i7-13700HX · 16\"",
     "V3.x alt sürümlerinin hepsi için geçerli. Abra A7 V16.2 ile aynı Kontrol Merkezi (v7.075) paketini kullanıyor."),
    # --- desteklenmeyenler
    ("Tulpar T7 V20.6", "Tulpar T7", "Tongfang GM7PG0R", "Tek bölge RGB", NO, "i7-13700H · RTX 4060",
     "Bu model Clevo değil **Tongfang** kasası; klavye ışığı farklı bir denetleyiciyle (Uniwill) sürülüyor. Program bunu algılar ve \"Tongfang (Uniwill) tabanlı model — henüz desteklenmiyor\" yazar."),
    ("Tulpar T5 V18 ve V19", "Tulpar T5", "Tongfang GK5CN6Z (muhtemel)", "RGB", NO, "i7-9750H / i7-10750H",
     "Bu seri büyük ihtimalle **Tongfang** kasası; desteklenmiyor."),
    ("Tulpar T7 V21.5", "Tulpar T7", "—", "4 bölge RGB", NO, "i7-10875H · RTX 3060", "4 bölgeli klavye; desteklenmiyor / denenmedi."),
    ("Tulpar T7 V26", "Tulpar T7", "—", "4 bölge RGB", NO, "2025–26 · RTX 5060", "4 bölgeli klavye; desteklenmiyor / denenmedi."),
    ("Semruk S7 ve S8", "Semruk", "—", "Tuş başına RGB", NO, "", "Her tuşu ayrı renk yanan klavye; bu program desteklemiyor."),
    ("Huma H4, H5, H7", "Huma", "—", "Tek renk (beyaz)", NO, "", "Klavye ışığı tek renk (beyaz); rengi değiştirilemez."),
    ("Markut M7", "Markut", "—", "Tek renk (beyaz)", NO, "", "Klavye ışığı tek renk (beyaz); rengi değiştirilemez."),
]

def slug(name):
    s = name.lower()
    for a, b in {"ı": "i", "ş": "s", "ğ": "g", "ü": "u", "ö": "o", "ç": "c"}.items():
        s = s.replace(a, b)
    s = s.replace(".", "-")
    s = re.sub(r"[^a-z0-9]+", "-", s).strip("-")
    return "monster-" + s

def issue_link(name):
    from urllib.parse import quote
    return f"{REPO}/issues/new?template=model-raporu.yml&title={quote('[Model] Monster ' + name)}"

pages = []
for name, series, chassis, kb, st, hw, note in M:
    full = f"Monster {name}"
    fn = slug(name) + ".md"
    pages.append((name, series, kb, st, fn))
    ok = st != NO
    lines = []
    if ok:
        lines += [
            f"# {full} — Klavye Işığı Renk Değiştirme Programı",
            "",
            f"**{full}** klavye ışığının rengini değiştirmek ve efekt eklemek (gökkuşağı, nefes, müziğe göre, ekran rengine göre, yazınca parlayan…) için ücretsiz, Türkçe program: **[Klavye Işık Stüdyosu]({REPO})**.",
            "",
            f"**[⬇️ {full} için indir]({DL})**",
            "",
        ]
    else:
        lines += [
            f"# {full} — Klavye Işığı Desteği",
            "",
            f"**{full}** şu an **Klavye Işık Stüdyosu** tarafından desteklenmiyor. Nedeni aşağıda.",
            "",
        ]
    lines += [
        "| | |",
        "|---|---|",
        f"| **Model** | {full} |",
    ]
    if hw:
        lines.append(f"| **Donanım** | {hw} |")
    lines += [
        f"| **Kasa** | {chassis} |",
        f"| **Klavye ışığı** | {kb} |",
        f"| **Durum** | {STATUS[st]} |",
    ]
    if ok:
        lines.append("| **Bağlantı** | Monster Kontrol Merkezi sürücüsü üzerinden, yönetici izni gerekmez |")
    lines.append("")
    if note:
        lines += ["## Not", "", note, ""]
    if ok:
        lines += [
            "## Kurulum",
            "",
            f"1. [Sürümler]({DL}) sayfasından `KlavyeIsik-…-win-x64.zip` dosyasını indir.",
            "2. Zip'i bir klasöre **çıkart** (zip'in içinden çalıştırma).",
            "3. `KlavyeIsik.exe`'yi aç. Kurulum, .NET ya da yönetici izni gerekmez.",
            "4. Bilgisayarda Monster **Kontrol Merkezi** kurulu olmalı (Fn tuşlarıyla klavye ışığı değişiyorsa kuruludur).",
            "",
            "Program açılınca üst satırda modelin ve klavye türü görünür. Her şey yolundaysa şöyle bir yazı görürsün:",
            "",
            f"> {full} · Tek bölge RGB · Doğrudan sürücü bağlantısı",
            "",
            "## Windows programı engellerse",
            "",
            "Program imzasız olduğu için Windows ilk açılışta uyarabilir; bu bir virüs uyarısı değildir.",
            "",
            "- **\"Windows kişisel bilgisayarınızı korudu\"** → **Ek bilgi** → **Yine de çalıştır**",
            "- **Düğme yoksa / açılmıyorsa** → zip'e sağ tık → **Özellikler** → **Engellemeyi kaldır** → Tamam → yeniden çıkart",
            "- **\"Tehdit bulundu\" / exe kayboldu** → Windows Güvenliği → Virüs ve tehdit koruması → **Koruma geçmişi** → **Geri yükle / İzin ver**",
            "",
            f"Ayrıntılı anlatım: [Windows programı engelliyorsa]({REPO}#windows-programı-engelliyorsa)",
            "",
            "## Neler yapabilirsin",
            "",
            "- 35 efekt: Sabit, Nefes, Gökkuşağı, Neon, Kuzey Işıkları, Ateş, Matrix, Okyanus, Pastel…",
            "- Müziğe göre yanan efektler, yazınca parlayan efektler",
            "- Ekranın rengine ya da ekranın alt kısmına göre ambiyans ışığı",
            "- Parlaklık, hız, iki renk seçimi; kilitte ışığı kapatma; Windows ile başlatma",
            "- Çok düşük işlemci kullanımı (Eko / Dengeli / Akıcı modlar)",
            "",
            "## Bu modelde denedin mi?",
            "",
            f"Çalıştıysa ya da sorun çıktıysa **[buradan bildir]({issue_link(name)})** — listeyi senin sonucuna göre güncelleriz.",
            "",
        ]
    else:
        lines += [
            "## Bu modelde denedin mi?",
            "",
            f"Programı açınca üst satırda ne yazdığını **[buradan bildir]({issue_link(name)})**. Gelen bilgilere göre destek eklenebilir.",
            "",
        ]
    lines += [f"[← Tüm modeller]({REPO}#modelini-bul)", ""]
    with open(os.path.join(out_dir, fn), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))

# ---- README için dizin
idx = ["## Modelini bul", "",
       "Modeline tıkla; kurulum ve o modele özel bilgiler ayrı sayfada. **Hepsi için indirme dosyası aynı** — program modeli kendisi tanır.",
       ""]
series_order = []
for p in pages:
    if p[3] != NO and p[1] not in series_order:
        series_order.append(p[1])
for s in series_order:
    items = [p for p in pages if p[1] == s and p[3] != NO]
    idx.append(f"**Monster {s}:** " + " · ".join(f"[{p[0].replace(s + ' ', '')}](modeller/{p[4]})" for p in items))
    idx.append("")
idx += [
    "| Model | Klavye | Durum |",
    "|---|---|---|",
]
for p in pages:
    if p[3] != NO:
        idx.append(f"| [Monster {p[0]}](modeller/{p[4]}) | {p[2]} | {SHORT[p[3]]} |")
idx += ["",
        "Monster'ın sitesinde \"**RGB Tek Bölge**\" yazan Clevo tabanlı diğer modellerde de çalışması beklenir. "
        f"Modelin listede yoksa ya da denediysen **[sonucu bildir]({REPO}/issues/new?template=model-raporu.yml)**.",
        "",
        "### Desteklenmeyen modeller",
        "",
        "| Model | Klavye | Neden |",
        "|---|---|---|"]
reason = {"Tongfang": "Tongfang kasa (farklı denetleyici)", "4 bölge": "4 bölgeli klavye", "Tuş başına": "Tuş başına RGB", "Tek renk": "Tek renk, renk değişmez"}
for name, series, chassis, kb, st, hw, note in M:
    if st != NO:
        continue
    r = next((v for k, v in reason.items() if k in chassis or k in kb), "Desteklenmiyor")
    idx.append(f"| [Monster {name}](modeller/{slug(name)}.md) | {kb} | {r} |")
idx += ["",
        "3 bölgeli eski Clevo klavyeler deneysel olarak desteklenir (üç bölge aynı renge ayarlanır).",
        ""]
intro = ("Program açıldığında modeli ve klavye türünü BIOS'tan **kendisi okur** ve üst satırda gösterir "
         "(örn. `Monster Abra A5 V16.7 · Tek bölge RGB · Doğrudan sürücü bağlantısı`).")
idx.insert(2, intro)
idx.insert(3, "")
readme = os.path.join(root, "README.md")
text = open(readme, encoding="utf-8").read()
a = text.index("## Modelini bul")
b = text.index("## Windows programı engelliyorsa")
NL = chr(10)
text = text[:a] + NL.join(idx).rstrip() + NL + NL + text[b:]
with open(readme, "w", encoding="utf-8", newline=NL) as f:
    f.write(text)
print(len(pages), "model sayfası yazıldı, README güncellendi")
