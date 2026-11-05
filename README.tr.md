<p align="center"><img src="assets/brand/tm-publisher-logo-horizontal-dark.svg" alt="TM Publisher" width="420"></p>

[English](README.md) · **Türkçe**

TM Publisher, tek bir gönderiyi kendi bilgisayarınızdan birçok sosyal medya hesabına yayınlayan ücretsiz ve açık kaynak (MIT)
bir masaüstü programıdır. Windows ve Linux'ta çalışır, her şeyi şifreli yerel bir veritabanında tutar ve platformlarla yalnız
**sizin kendi geliştirici uygulamanız ve anahtarlarınızla** konuşur. Sunucu, ortak uygulama ya da bizde bir hesap yoktur.

Desteklenenler: YouTube · X · TikTok · Instagram · Facebook Sayfası · Threads · Bluesky · Mastodon · Telegram · Discord · Pinterest · LinkedIn.

- [Ne yapar](#ne-yapar)
- [Windows'a kurulum](#windowsa-kurulum)
- [Linux'a kurulum (x64 ve arm64)](#linuxa-kurulum-x64-ve-arm64)
- [Programı ilk kez açınca](#programı-ilk-kez-açınca)
- [Hesaplarınızı bağlayın](#hesaplarınızı-bağlayın)
- [Güncelleme](#güncelleme)
- [Kaldırma](#kaldırma)
- [Verileriniz nerede](#verileriniz-nerede)
- [Bilinen kısıtlar](#bilinen-kısıtlar)
- [Kaynaktan derleme](#kaynaktan-derleme)

## Ne yapar

- **Bir kez yaz, her yere gönder.** Metin, fotoğraf ya da video; her hesap alacağı metni tam olarak gösterir (platform
  etiketleri, başlık, uzunluk sayacı) ve göndermeden önce çalışan denetimlerin aynısı ekranda görünür. Formda ne görünüyorsa
  o yayınlanır.
- **Gruplar.** Kayıtlı bir hesap grubuna kendi sırasıyla gönderin ya da hesapları elle seçin. Gönderi asla hiçbir yere
  gitmeden kalmaz.
- **Planlama.** Tek zamanlayıcı, yoklama yok. Hesaplara tek tek gönderilir; biri hata verirse kalanlar kararınızı bekler
  (yeniden dene, atla ve devam et, iptal). En çok 5 dakika gecikmiş gönderi yine gider; bilgisayar kapalıyken kaçırılan
  gönderi **Kararınızı bekleyen** sayfasında sizi bekler.
- **Takvim.** Ay ve hafta görünümü; planlı bir gönderiyi başka bir güne sürükleyin.
- **Geçmiş.** Yayınlananların bağlantıları, yerel sayılar ve sınırlar. Platform sayıları (izlenme, beğeni) yalnız **Sayıları
  getir**'e tıklayınca okunur; X'in okuma ücreti önce gösterilir.
- **Bildirimler** sağ alt köşede (yayınlandı, onayınız gerekiyor, hata). Varsayılan bir dakika; Ayarlar'dan süreyi
  değiştirebilir ya da kapatabilirsiniz.
- **Şifreli.** Veritabanı şifrelidir (SQLite3 Multiple Ciphers). Anahtarını Windows (DPAPI) ya da Linux anahtarlığı veya
  seçeceğiniz bir ana parola korur. Yedek, parolalı tek bir dosyadır (Argon2id + AES-256-GCM).
- **İki dil:** Türkçe ve İngilizce.

Mesaj ya da yorum **okumaz**, e-posta göndermez.

## Windows'a kurulum

Windows 10 ya da 11, 64 bit.

1. [Son sürüm](../../releases/latest) sayfasını açın ve `TMPublisher-<sürüm>-win-x64.exe` dosyasını indirin.
2. İsteğe bağlı ama önerilir: dosyayı sürüm sayfasındaki SHA-256 değeriyle karşılaştırın. PowerShell'de:
   ```powershell
   Get-FileHash .\TMPublisher-0.1.0-win-x64.exe -Algorithm SHA256
   ```
3. Dosyayı kalacağı bir yere koyun, örneğin `%LOCALAPPDATA%\Programs\TM Publisher\TMPublisher.exe`. Otomatik başlatma bu
   yola bakar; yeri ilk açılıştan önce seçin.
4. Çift tıklayın. Dosya kod imzalı olmadığından Windows **"Windows kişisel bilgisayarınızı korudu"** (SmartScreen) uyarısını
   gösterir: **Ek bilgi**'ye, ardından **Yine de çalıştır**'a tıklayın. Windows genellikle yalnız ilk seferde sorar.
5. [Programı ilk kez açınca](#programı-ilk-kez-açınca) ile devam edin.

Yönetici izni gerekmez; sisteme bir şey kurulmaz.

## Linux'a kurulum (x64 ve arm64)

Sıradan bilgisayarlarda `linux-x64`, 64 bit ARM bilgisayarlarda (örneğin 64 bit masaüstüyle Raspberry Pi 4 ya da 5)
`linux-arm64` kullanın. Bir masaüstü oturumu (X11 ya da XWayland'li Wayland) gerekir.

1. [Son sürüm](../../releases/latest) sayfasından `tm-publisher-<sürüm>-linux-x64.tar.gz` (ya da `-linux-arm64`) dosyasını indirin.
2. İsteğe bağlı: SHA-256 değerini denetleyin:
   ```sh
   sha256sum tm-publisher-0.1.0-linux-x64.tar.gz
   ```
3. Kalacağı yere açın ve çalıştırma izni verin:
   ```sh
   mkdir -p ~/.local/opt
   tar -xzf tm-publisher-0.1.0-linux-x64.tar.gz -C ~/.local/opt
   chmod +x ~/.local/opt/tm-publisher/tm-publisher
   ```
4. **Anahtarlık (önerilir).** Veritabanının oturum açınca kendiliğinden açılması için TM Publisher anahtarını masaüstü
   anahtarlığınıza (GNOME Keyring, KWallet, KeePassXC...) `secret-tool` komutuyla koyar:
   - Debian, Ubuntu, Raspberry Pi OS: `sudo apt install libsecret-tools`
   - Fedora: `sudo dnf install libsecret`
   - Arch: `sudo pacman -S libsecret`

   Anahtarlık yoksa program yine çalışır: veritabanını her açılışta sorduğu bir ana parolayla korur.
5. Başlatın: `~/.local/opt/tm-publisher/tm-publisher`, ardından [Programı ilk kez açınca](#programı-ilk-kez-açınca) ile devam edin.

Notlar:

- **Tepsi simgesi:** StatusNotifierItem / AppIndicator desteği ister. KDE, XFCE, Cinnamon ve çoğu panelde var; GNOME'da
  "AppIndicator and KStatusNotifierItem Support" eklentisini kurun. Tepsi yoksa pencereyi kapatmak onu küçültür.
- **Otomatik başlatma:** program `~/.config/autostart/tm-publisher.desktop` dosyasını yazar (XDG autostart kuralına uyan
  masaüstleri oturum açılınca başlatır). Ayarlar'dan açıp kapatın.
- Program açılmaz ve ICU'dan söz ederse dağıtımınızın `libicu` paketini kurun.

## Programı ilk kez açınca

1. **Dil:** Türkçe ya da İngilizce (sonra Ayarlar'dan değişir).
2. **Sıfırdan başla** ya da **Yedekten getir** (`.tmpbak` dosyası ve parolası).
3. **Koruma:** varsayılan olarak veritabanı anahtarını bu bilgisayar (Windows) ya da anahtarlık (Linux) korur ve kendiliğinden
   açılır. Bunun yerine bir **ana parola** seçebilirsiniz; o zaman her açılışta sorulur. Unutulan ana parola kurtarılamaz,
   bu yüzden yedek alın.
4. **Bilgisayarımla birlikte başlasın (tepside)** varsayılan olarak açıktır: TM Publisher tepside başlar, planlı gönderiler zamanında gider.
   İsterseniz Ayarlar'dan kapatın.
5. **Hesaplar** altında hesaplarınızı ekleyin (aşağıda), isterseniz **Gruplar** oluşturun, sonra **Yeni gönderi**'de ilk
   gönderinizi yazın.

Hesapları bağladıktan sonra **Ayarlar → Yedek**'ten bir yedek alın. Yedek şifreli veritabanını (hesaplar, anahtarlar,
gruplar, gönderiler, ayarlar) içerir; medya dosyalarınızı içermez.

## Hesaplarınızı bağlayın

Her platform **sizin kendi** geliştirici uygulamanız ya da anahtarınızla bağlanır. **Hesap ekle** penceresi bu rehberlerdeki
adımların aynısını, her biri işaretlenebilir şekilde gösterir; gereken yerde kopyalanacak dönüş adresini verir ve
**Bağla ve dene** düğmesi işin olup olmadığını hemen söyler.

| Platform | Neler gönderilebilir | Nasıl bağlanır | Rehber |
|---|---|---|---|
| YouTube | Bir video | Google Cloud "Masaüstü uygulaması" istemciniz | [Türkçe](docs/platforms/youtube.tr.md) · [English](docs/platforms/youtube.md) |
| X | Metin, en çok 4 fotoğraf ya da bir video (gönderi başına ücretli) | X uygulamanız (OAuth 2.0, sırsız) | [Türkçe](docs/platforms/x.tr.md) · [English](docs/platforms/x.md) |
| TikTok | Bir video; TikTok gelen kutunuza taslak (ya da gizli) | TikTok uygulamanız (Masaüstü için Login Kit) | [Türkçe](docs/platforms/tiktok.tr.md) · [English](docs/platforms/tiktok.md) |
| Instagram | Bir video (Reels) | Meta uygulamanız + Sayfaya bağlı profesyonel hesap | [Türkçe](docs/platforms/instagram.tr.md) · [English](docs/platforms/instagram.md) |
| Facebook Sayfası | Metin, bağlantı, fotoğraf, bir video | Meta uygulamanız + Graph API Explorer anahtarı | [Türkçe](docs/platforms/facebook.tr.md) · [English](docs/platforms/facebook.md) |
| Threads | Metin | Threads uygulamanız (dönüş adresini yapıştırın) | [Türkçe](docs/platforms/threads.tr.md) · [English](docs/platforms/threads.md) |
| Bluesky | Metin, en çok 4 fotoğraf ya da bir video | Uygulama şifresi | [Türkçe](docs/platforms/bluesky.tr.md) · [English](docs/platforms/bluesky.md) |
| Mastodon | Metin, en çok 4 fotoğraf ya da bir video | Program sunucunuza kendini kaydeder | [Türkçe](docs/platforms/mastodon.tr.md) · [English](docs/platforms/mastodon.md) |
| Telegram | Metin, fotoğraf, video, albüm | Botunuz (BotFather) | [Türkçe](docs/platforms/telegram.tr.md) · [English](docs/platforms/telegram.md) |
| Discord | Metin ve dosya | Kanal webhook'u | [Türkçe](docs/platforms/discord.tr.md) · [English](docs/platforms/discord.md) |
| Pinterest | Bir resim ya da video pini | Pinterest uygulamanız (dönüş adresini yapıştırın) | [Türkçe](docs/platforms/pinterest.tr.md) · [English](docs/platforms/pinterest.md) |
| LinkedIn | Profilinizde metin, fotoğraf ya da bir video | LinkedIn token üreticisinden anahtar (60 gün) | [Türkçe](docs/platforms/linkedin.tr.md) · [English](docs/platforms/linkedin.md) |

## Güncelleme

1. İsteğe bağlı: **Ayarlar → Yedek**'ten yedek alın.
2. TM Publisher'dan tepsi menüsüyle çıkın (**Çık**); pencereyi kapatmak yalnız gizler.
3. Program dosyasını yenisiyle değiştirin (aynı yer ve ad; otomatik başlatma çalışmaya devam eder). Linux'ta yeni arşivi eski
   klasörün üstüne açın ve `chmod +x`'i yeniden çalıştırın.
4. Başlatın. Verileriniz yerinde kalır.

## Kaldırma

1. **Ayarlar**'da **Bilgisayarımla birlikte başlasın**'ı kapatın (Windows'ta otomatik başlatma kaydını siler, Linux'ta devre dışı bırakır).
2. Tepsi menüsünden çıkın.
3. Program dosyasını (Windows) ya da `tm-publisher` klasörünü (Linux) silin; Linux'ta
   `~/.config/autostart/tm-publisher.desktop` dosyasını da silin.
4. Verilerinizi de kaldırmak için aşağıdaki veri klasörünü silin. Bu geri alınamaz; gerekebilecekse önce yedek alın.
   Linux'ta anahtarlık kullanıyorsanız anahtarlık uygulamanızdaki (Seahorse, KWallet...) "TM Publisher database key"
   kaydını da silin.

## Verileriniz nerede

| | Klasör |
|---|---|
| Windows | `%LOCALAPPDATA%\TMPublisher` |
| Linux | `~/.local/share/tm-publisher` (ya da `$XDG_DATA_HOME/tm-publisher`) |

İçinde: `publisher.db` (şifreli veritabanı), `key.json` (DPAPI, anahtarlık ya da ana parolanızla mühürlenmiş veritabanı
anahtarı), `preferences.json` (yalnız arayüz dili), `logs/` (14 gün tutulur; asla anahtar ya da parola içermez). Medya
dosyaları sizin tuttuğunuz yerde kalır; yalnız yolları saklanır.

Başka bir klasör `--data-dir <klasör>` ya da `TMPUBLISHER_DATA_DIR` ortam değişkeniyle kullanılabilir. Program ilk açılışta
yerel kitaplıklarını sistemin geçici klasörüne açar (standart .NET tek dosya davranışı).

## Bilinen kısıtlar

- **TikTok:** gönderiler TikTok gelen kutunuza taslak olarak gider ve uygulamada tamamlarsınız; isteğe bağlı doğrudan paylaşım
  gizlidir. TikTok "kendi hesabıma yükleme" araçlarını denetimden geçirmediği için herkese açık doğrudan paylaşım kapalıdır.
- **Pinterest:** deneme (Trial) erişiminde pinleriniz yalnız size görünür; standart erişim için Pinterest'in uygulamanızı
  incelemesi gerekir.
- **Instagram ve Threads:** bu sürümde yalnız video (Instagram) ve metin (Threads); fotoğrafları herkese açık bir web
  adresinden gelmek zorunda.
- **LinkedIn:** anahtar 60 gün sürer; program hatırlatınca yeniden bağlayın.
- **X** gönderi (yaklaşık 0,015 $, bağlantılı 0,20 $) ve okuma başına ücret alır; yazma ekranı ve Sayıları getir düğmesi tahmini önce
  gösterir.
- Mesaj ya da yorum okuma yok, e-posta yok.

## Kaynaktan derleme

.NET 10 SDK ve Python 3 (git kancaları için) gerekir.

```sh
git clone <bu depo>
cd tm-publisher
tools/install-hooks.sh        # ya da: pwsh tools/install-hooks.ps1
dotnet test TMPublisher.slnx
dotnet run --project src/TMPublisher.Desktop
```

Sürüm dosyaları: `pwsh tools/publish.ps1` ya da `tools/publish.sh` (`artifacts/release` altında bağımsız tek dosyalar,
SHA-256 değerleri yazdırılır). `docs/platforms` altındaki platform rehberleri programın kendi metinlerinden
`python tools/gen-platform-docs.py` ile üretilir.

## Lisans

MIT, bkz. [LICENSE](LICENSE). Üçüncü taraf malzeme: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
