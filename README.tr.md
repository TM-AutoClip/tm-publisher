<p align="center"><img src="assets/brand/tm-publisher-logo-horizontal-dark.svg" alt="TM Publisher" width="420"></p>

[English](README.md) · **Türkçe**

TM Publisher, tek bir gönderiyi kendi bilgisayarınızdan birçok sosyal medya hesabına yayınlayan ücretsiz ve açık kaynak (MIT)
bir masaüstü programıdır. Windows ve Linux'ta çalışır, her şeyi şifreli yerel bir veritabanında tutar ve platformlarla yalnız
**sizin kendi geliştirici uygulamanız ve anahtarlarınızla** konuşur. Sunucu, ortak uygulama ya da bizde bir hesap yoktur.

Desteklenenler: YouTube · X · TikTok · Instagram · Facebook Sayfası · Threads · Bluesky · Mastodon · Telegram · Discord · Pinterest · LinkedIn.

- [Ne yapar](#ne-yapar)
- [Arka planda ne çalışır](#arka-planda-ne-çalışır)
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
- **Planlama.** Küçük bir arka plan zamanlayıcısı gönderilerinizi zamanında gönderir, pencere kapalıyken de (bkz.
  [Arka planda ne çalışır](#arka-planda-ne-çalışır)). Tek zamanlayıcı, yoklama yok. Hesaplara tek tek gönderilir; biri hata
  verirse kalanlar kararınızı bekler (yeniden dene, atla ve devam et, iptal). En çok 5 dakika gecikmiş gönderi yine gider;
  bilgisayar kapalıyken kaçırılan gönderi **Kararınızı bekleyen** sayfasında sizi bekler.
- **Takvim.** Ay ve hafta görünümü; planlı bir gönderiyi başka bir güne sürükleyin.
- **Geçmiş.** Yayınlananların bağlantıları, yerel sayılar ve sınırlar. Platform sayıları (izlenme, beğeni) yalnız **Sayıları
  getir**'e tıklayınca okunur; X'in okuma ücreti önce gösterilir.
- **Bildirimler** sağ alt köşede (yayınlandı, onayınız gerekiyor, hata), pencere kapalıyken de. Varsayılan bir dakika;
  Ayarlar'dan süreyi değiştirebilir ya da kapatabilirsiniz.
- **Şifreli.** Veritabanı şifrelidir (SQLite3 Multiple Ciphers). Anahtarını Windows (DPAPI) ya da Linux anahtarlığı veya
  seçeceğiniz bir ana parola korur. Yedek, parolalı tek bir dosyadır (Argon2id + AES-256-GCM).
- **İki dil:** Türkçe ve İngilizce.

Mesaj ya da yorum **okumaz**, e-posta göndermez.

## Arka planda ne çalışır

TM Publisher aynı klasörde duran iki programdır:

| | Windows | Linux | Ne zaman çalışır |
|---|---|---|---|
| Pencere | `TMPublisher.exe` | `tm-publisher` | yalnız siz açıkken; kapatınca kapanır |
| Arka plan zamanlayıcısı | `TMPublisherScheduler.exe` | `tm-publisher-scheduler` | her zaman: sıradaki gönderinin saatini bekler ve gönderir |

Zamanlayıcı küçüktür: pencere kitaplığı yoktur, yerel koda derlenmiştir. 0.1.3 için gönderi beklerken ölçülen: Windows Görev
Yöneticisi'nde yaklaşık 6 MB (çalışma kümesi 22 MB), Raspberry Pi 5'te yaklaşık 22 MB bellek. Bir gönderiyi gönderirken kısa bir
süre daha fazla kullanır; bildirimi, pencere programının bildirim kapanınca kendiliğinden kapanan kısa ömürlü bir kopyası gösterir.

- **Başlaması:** bilgisayarınızla birlikte (Ayarlar → **Arka plan zamanlayıcısı bilgisayarımla birlikte başlasın**, varsayılan
  açık) ve pencereyi her açtığınızda. Bir veri klasörü için yalnız bir zamanlayıcı çalışır.
- **Görmek ve yönetmek:** Ayarlar → **Arka plan zamanlayıcısı** çalışıp çalışmadığını ve sıradaki gönderiyi gösterir; Başlat,
  Durdur, Duraklat ve Sürdür düğmeleri oradadır. Windows'ta bir de tepsi simgesi vardır: tıklayınca pencere açılır; sağ tıkta
  Zamanlamayı duraklat / sürdür ve Çık.
- **Durdurmak:** Ayarlar → **Durdur** ya da Windows'ta tepsi simgesi → **Çık (zamanlamayı durdur)**. Durdurulmuşken gönderi
  gitmez; pencereyle ya da bilgisayarla yeniden başlar. Bilgisayarla başlamasın istiyorsanız ayarı kapatın. O an gönderilen
  gönderi önce bitirilir.
- **Duraklatmak** zamanlayıcıyı çalışır bırakır ama hiçbir şey göndermez; sürdürmeden önce saati 5 dakikadan fazla geçen
  gönderiler sizi bekler.
- **Ana parola:** veritabanını bir ana parolayla koruyorsanız (Linux'ta anahtarlık yoksa, örneğin Raspberry Pi'de, her zaman
  böyledir) zamanlayıcı parolayı soramaz. Bilgisayar açıldıktan sonra TM Publisher'ı bir kez açıp kilidini açın: pencere anahtarı
  zamanlayıcıya yalnız sizin kullanıcınızın açabildiği bir kanaldan verir (komut satırına ya da bir dosyaya asla yazılmaz). O
  zamana kadar Ayarlar'da "Ana parola bekleniyor" yazar ve hiçbir şey gönderilmez.

## Windows'a kurulum

Windows 10 ya da 11, 64 bit.

1. [Son sürüm](../../releases/latest) sayfasını açın ve `TMPublisher-<sürüm>-win-x64.zip` dosyasını indirin.
2. İsteğe bağlı ama önerilir: dosyayı sürüm sayfasındaki SHA-256 değeriyle karşılaştırın. PowerShell'de:
   ```powershell
   Get-FileHash .\TMPublisher-0.1.3-win-x64.zip -Algorithm SHA256
   ```
3. Kalacağı bir yere açın, örneğin `%LOCALAPPDATA%\Programs` içine. İçinden `TMPublisher.exe`, `TMPublisherScheduler.exe` ve
   `sqlite3mc.dll` bulunan bir `TM Publisher` klasörü çıkar; üçünü bir arada tutun. Otomatik başlatma tam bu klasöre bakar; yeri
   ilk açılıştan önce seçin.
4. `TMPublisher.exe`'ye çift tıklayın. Dosyalar kod imzalı olmadığından Windows **"Windows kişisel bilgisayarınızı korudu"**
   (SmartScreen) uyarısını gösterir: **Ek bilgi**'ye, ardından **Yine de çalıştır**'a tıklayın. Zamanlayıcı için bir kez daha
   sorabilir.
5. [Programı ilk kez açınca](#programı-ilk-kez-açınca) ile devam edin.

Yönetici izni gerekmez; sisteme bir şey kurulmaz.

## Linux'a kurulum (x64 ve arm64)

Sıradan bilgisayarlarda `linux-x64`, 64 bit ARM bilgisayarlarda (örneğin 64 bit masaüstlü Raspberry Pi 4 ya da 5) `linux-arm64`
kullanın. Pencere ve bildirimler için bir masaüstü oturumu (X11 ya da XWayland'li Wayland) gerekir.

1. [Son sürüm](../../releases/latest) sayfasından `tm-publisher-<sürüm>-linux-x64.tar.gz` (ya da `-linux-arm64`) dosyasını
   indirin.
2. İsteğe bağlı: SHA-256 değerini denetleyin:
   ```sh
   sha256sum tm-publisher-0.1.3-linux-x64.tar.gz
   ```
3. Kalacağı bir yere açın (programlar zaten çalıştırılabilir işaretli):
   ```sh
   mkdir -p ~/.local/opt
   tar -xzf tm-publisher-0.1.3-linux-x64.tar.gz -C ~/.local/opt
   ```
4. **Anahtarlık (önerilir).** Oturum açınca veritabanının kendiliğinden açılması için TM Publisher anahtarını masaüstü
   anahtarlığınızda (GNOME Keyring, KWallet, KeePassXC...) `secret-tool` komutuyla saklar:
   - Debian, Ubuntu, Raspberry Pi OS: `sudo apt install libsecret-tools`
   - Fedora: `sudo dnf install libsecret`
   - Arch: `sudo pacman -S libsecret`

   Anahtarlık olmadan da çalışır: veritabanını bir ana parolayla korur; bilgisayar her açıldıktan sonra pencerenin kilidini bir
   kez açarsınız (bkz. [Arka planda ne çalışır](#arka-planda-ne-çalışır)).
5. Başlatın: `~/.local/opt/tm-publisher/tm-publisher`, sonra [Programı ilk kez açınca](#programı-ilk-kez-açınca) ile devam edin.
   Pencere için menü girdisi isteğe bağlıdır (örneğin `~/.local/share/applications` içinde `Exec=` satırı `tm-publisher`'ın tam
   yolu olan bir `.desktop` dosyası).

Notlar:

- **Linux'ta tepsi simgesi yok:** zamanlayıcının yoktur (tepsi her masaüstünde bulunmaz). Pencereyi menünüzden ya da komut
  satırından açın; Ayarlar → Arka plan zamanlayıcısı durumunu gösterir ve onu durdurur.
- **Otomatik başlatma:** pencere `~/.config/autostart/tm-publisher.desktop` dosyasını yazar; XDG otomatik başlatma kuralına
  uyan masaüstlerinde zamanlayıcı oturum açılınca başlar. Ayarlar'dan açıp kapatın.
- **linux-x64:** bu sürümde x64 zamanlayıcısı yerel koda derlenmedi (bu yalnız bir x64 Linux makinesinde yapılabiliyor; sürüm
  orada derlenmedi). Onun yerine kırpılmış, önceden derlenmiş tek dosyadır ve arm64'tekinin yaklaşık iki katı bellek kullanır
  (arm64'te ölçüldü: 22 MB yerine yaklaşık 46 MB). Gerisi aynıdır.

## Programı ilk kez açınca

1. **Dil:** Türkçe ya da İngilizce (sonra Ayarlar'dan değişir).
2. **Sıfırdan başla** ya da **Yedekten getir** (`.tmpbak` dosyası ve parolası).
3. **Koruma:** varsayılan olarak veritabanı anahtarını bu bilgisayar (Windows) ya da anahtarlık (Linux) korur ve kendiliğinden
   açılır. Bunun yerine bir **ana parola** seçebilirsiniz; o zaman her açılışta sorulur. Unutulan ana parola kurtarılamaz,
   bu yüzden yedek alın.
4. **Arka plan zamanlayıcısı bilgisayarımla birlikte başlasın** varsayılan olarak açıktır; planlı gönderiler pencere kapalıyken
   de zamanında gider. İsterseniz Ayarlar'dan kapatın.
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

1. İsteğe bağlı: **Ayarlar → Yedek**'ten bir yedek alın.
2. Eski sürümü durdurun:
   - **0.1.0–0.1.2'den:** tepsi menüsünden (**Çık**) ya da kenar çubuğunun altındaki **TM Publisher'dan çık** ile kapatın (pencereyi
     kapatmak onu yalnız gizliyordu); ona giden eski kısayolları ve sabitlemeleri de kaldırın. Yeni zamanlayıcı aynı veri
     klasöründe eski bir sürüm bulduğu sürece başlamaz ve hiçbir gönderiyi üstüne almaz; Windows'ta sonradan başlatılan eski sürüm
     hemen kapanır. Linux'ta yeni zamanlayıcıdan sonra başlatılan eski sürüm, siz onu kapatana kadar gönderileri kendisi yollar
     (yeni olan bekler) ve yeninin o anda gönderdiği bir gönderi "yarıda kaldı" olarak dönebilir. Bu yüzden önce eski sürümü kapatın.
   - **0.1.3 ve sonrasından:** pencereyi kapatın ve zamanlayıcıyı durdurun (Ayarlar → **Durdur** ya da tepsi simgesi → **Çık**).
3. Windows: yeni zip'i açın ve `TM Publisher` klasöründeki dosyaları değiştirin (aynı yer; otomatik başlatma çalışmaya devam eder).
   Tek bir `TMPublisher-<sürüm>-win-x64.exe` olan 0.1.0–0.1.2'den geliyorsanız: yeni klasörü istediğiniz yere koyun, oradaki
   `TMPublisher.exe`'yi başlatın ve eski exe'yi silin; otomatik başlatma kaydı yeni pencere açılınca yeni zamanlayıcıya taşınır.
   Linux: yeni arşivi eski klasörün üzerine açın.
4. Pencereyi başlatın. Verileriniz yerinde kalır.

## Kaldırma

1. **Ayarlar**'da **Arka plan zamanlayıcısı bilgisayarımla birlikte başlasın**'ı kapatın (Windows'ta otomatik başlatma kaydını
   siler, Linux'ta devre dışı bırakır), sonra oradan zamanlayıcıyı **Durdur**'un.
2. Pencereyi kapatın.
3. `TM Publisher` klasörünü (Windows) ya da `tm-publisher` klasörünü (Linux) silin; Linux'ta
   `~/.config/autostart/tm-publisher.desktop` dosyasını da silin.
4. Verilerinizi de kaldırmak için aşağıdaki veri klasörünü silin. Bu geri alınamaz; gerekebilecekse önce yedek alın.
   Linux'ta anahtarlık kullanıyorsanız anahtarlık uygulamanızdan (Seahorse, KWallet...) "TM Publisher database key" girdisini
   de silin.

## Verileriniz nerede

| | Klasör |
|---|---|
| Windows | `%LOCALAPPDATA%\TMPublisher` |
| Linux | `~/.local/share/tm-publisher` (ya da `$XDG_DATA_HOME/tm-publisher`) |

İçinde: `publisher.db` (şifreli veritabanı), `key.json` (DPAPI, anahtarlık ya da ana parolanızla mühürlenmiş veritabanı
anahtarı), `preferences.json` (yalnız arayüz dili), `logs/` (gün ve program başına bir dosya: pencere için `tmpublisher-…`,
zamanlayıcı için `tmpublisher-scheduler-…`, bildirimler için `tmpublisher-notify-…`; 14 gün tutulur; asla anahtar ya da parola
içermez), `scheduler-instance.lock` / `window-instance.lock` (bir veri klasörü için bir zamanlayıcı ve bir pencere),
`scheduler-paused.txt` (yalnız zamanlama duraklatılmışken) ve `locks/` (bir hesabın anahtar yenilemesi asla aynı anda iki kez
çalışmaz). Medya dosyaları sizin tuttuğunuz yerde kalır; yalnız yolları saklanır.

Başka bir klasör `--data-dir <klasör>` ya da `TMPUBLISHER_DATA_DIR` ortam değişkeniyle kullanılabilir; otomatik başlatma kaydı
o klasörü korur. Pencere programı ilk açılışta yerel kitaplıklarını sistemin geçici klasörüne açar (.NET tek dosya
programlarının olağan davranışı).

Pencere ekran kartıyla değil işlemciyle çizer. Çok büyük bir ekranda çizim yavaş gelirse `--gpu` ile başlatın (ya da
`TMPUBLISHER_GPU=1` ortam değişkenini tanımlayın).

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
dotnet run --project src/TMPublisher.Desktop     # pencere; zamanlayıcı src/TMPublisher.Background
```

Sürüm dosyaları yalnız temiz bir çalışma ağacından (programlar derlendikleri commit'i taşır): Windows'ta `pwsh tools/publish.ps1`
(win-x64: pencere kırpılmış tek dosya, zamanlayıcı Native AOT; Visual Studio'nun C++ araçları gerekir), Linux'ta
`tools/publish.sh` (zamanlayıcı makinenin kendi mimarisi için clang ya da gcc ile Native AOT, öteki mimari için kırpılmış
ReadyToRun tek dosya). Paketler ve `SHA256SUMS.txt` `artifacts/release` altına yazılır. `docs/platforms` altındaki platform rehberleri programın kendi metinlerinden
`python tools/gen-platform-docs.py` ile üretilir.

## Lisans

MIT, bkz. [LICENSE](LICENSE). Üçüncü taraf malzeme: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
