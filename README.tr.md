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
- **Planlama.** Gönderileriniz zamanında gider, program kapalıyken de: sisteminizin zamanlayıcısı TM Publisher'ı kısa bir süre için
  başlatır, o gönderir ve kapanır (bkz. [Arka planda ne çalışır](#arka-planda-ne-çalışır)). Yoklama yok. Hesaplara tek tek gönderilir; biri hata
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

Gönderileriniz arasında hiçbir şey. TM Publisher tek bir program dosyasıdır (Windows'ta `TMPublisher.exe`, Linux'ta `tm-publisher`):

- **Pencere açıkken** zamanlanmış gönderileri saatinde pencere gönderir.
- **Kapalıyken** sisteminizin zamanlayıcısı aynı dosyayı `--run-due` ile kısa bir süre için başlatır: veritabanını açar, saati
  gelen her gönderiyi gönderir, sonraki başlatmayı ayarlar ve kapanır. Windows'ta bu, kullanıcınızın bir **Görev Zamanlayıcı**
  görevidir (`TM Publisher <kod>`, Görev Zamanlayıcı Kitaplığı altında); Linux'ta bir **systemd kullanıcı zamanlayıcısı**
  (`tm-publisher-<kod>.timer`, `~/.config/systemd/user` içinde). Yönetici izni gerekmez. TM Publisher'ı sıradaki gönderinin saatinde ve o çalıştırma
  sonrakini ayarlamadan kapandıysa diye 30, 60 ve 120 dakika sonra başlatır (Windows'ta ayrıca oturum açtıktan bir dakika sonra);
  bilgisayar kapalı ya da uykudayken kaçan başlatma, bilgisayar dönünce hemen yapılır. 5 dakikadan fazla gecikmiş gönderi
  kendiliğinden gitmez: **Kararınızı bekleyen** sayfasında sizi bekler.
- Böyle bir çalıştırmanın **bildirimlerini** (yayınlandı, başarısız, kaçırıldı) sağ alt köşede, son bildirim kapanınca kapanan
  kısa ömürlü bir kopya gösterir.
- **Ayarlar → TM Publisher kapalıyken de zamanlanmış gönderileri gönder** (varsayılan açık) görevi / zamanlayıcıyı yazar ya da
  siler. Ayarlar → **Gönderme** şu an kimin gönderdiğini, sıradaki gönderiyi ve sistemin TM Publisher'ı ne zaman başlatacağını
  gösterir. **Bir gönderiyi göndermek için bilgisayarı uykudan uyandır** (Windows, varsayılan kapalı) görevin bilgisayarı
  uyandırmasına izin verir.
- **Zamanlamayı duraklat** (Ayarlar → Gönderme) hem pencereyi hem çalıştırmaları durdurur: hiçbir şey gönderilmez; sürdürmeden önce
  saati 5 dakikadan fazla geçen gönderiler sizi bekler.
- Bir veri klasöründe aynı anda yalnız bir gönderici çalışır: pencere açıkken çalıştırma hemen kapanır, iki çalıştırma asla üst üste
  gelmez.
- **Ana parola:** çalıştırma parolayı soramaz; bu yüzden ana parolayla (Linux'ta anahtarlık yoksa, örneğin Raspberry Pi'de, her
  zaman böyledir) program kapalıyken hiçbir şey gönderilmez. Bir gönderinin saati gelince bir bildirim TM Publisher'ı açmanızı bir
  kez ister; kilidi açtığınızda gönderiyi pencere gönderir.
- **systemd kullanıcı oturumu olmayan Linux:** TM Publisher onun yerine bir otomatik başlatma girdisi yazar
  (`~/.config/autostart/tm-publisher-<kod>.desktop`); saati gelen gönderiler oturum açınca gönderilir ve Ayarlar bunu söyler. Program
  kapalıyken gönderiler o zaman saatinde gitmez.

## Windows'a kurulum

Windows 10 ya da 11, 64 bit.

1. [Son sürüm](../../releases/latest) sayfasını açın ve `TMPublisher-<sürüm>-win-x64.zip` dosyasını indirin.
2. İsteğe bağlı ama önerilir: dosyayı sürüm sayfasındaki SHA-256 değeriyle karşılaştırın. PowerShell'de:
   ```powershell
   Get-FileHash .\TMPublisher-0.1.3-win-x64.zip -Algorithm SHA256
   ```
3. Kalacağı bir yere açın, örneğin `%LOCALAPPDATA%\Programs` içine. İçinden tek bir program (`TMPublisher.exe`) ve belgeleri
   bulunan bir `TM Publisher` klasörü çıkar. Görev Zamanlayıcı görevi tam bu dosyaya bakar; yeri ilk açılıştan önce seçin (sonra
   taşırsanız taşınan programı bir kez açın, görevi yeniden yazar).
4. `TMPublisher.exe`'ye çift tıklayın. Dosya kod imzalı olmadığından Windows **"Windows kişisel bilgisayarınızı korudu"**
   (SmartScreen) uyarısını gösterir: **Ek bilgi**'ye, ardından **Yine de çalıştır**'a tıklayın.
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
3. Kalacağı bir yere açın (program zaten çalıştırılabilir işaretli):
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

- **Kapalıyken gönderme** bir systemd kullanıcı zamanlayıcısıyla çalışır (bkz. [Arka planda ne çalışır](#arka-planda-ne-çalışır)).
  Siz oturum açmışken çalışır; kimse oturum açmamışken de çalışsın istiyorsanız bir kez `loginctl enable-linger $USER` deyin.
- Çalıştırma bildirimlerini yalnız masaüstü oturumunuzu biliyorsa gösterir (systemd kullanıcı ortamında `DISPLAY` /
  `WAYLAND_DISPLAY`; çoğu masaüstü bunu ayarlar); yoksa yalnız günlüğe yazılır.

## Programı ilk kez açınca

1. **Dil:** Türkçe ya da İngilizce (sonra Ayarlar'dan değişir).
2. **Sıfırdan başla** ya da **Yedekten getir** (`.tmpbak` dosyası ve parolası).
3. **Koruma:** varsayılan olarak veritabanı anahtarını bu bilgisayar (Windows) ya da anahtarlık (Linux) korur ve kendiliğinden
   açılır. Bunun yerine bir **ana parola** seçebilirsiniz; o zaman her açılışta sorulur. Unutulan ana parola kurtarılamaz,
   bu yüzden yedek alın.
4. **TM Publisher kapalıyken de zamanlanmış gönderileri gönder** varsayılan olarak açıktır; planlı gönderiler program kapalıyken
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
     kapatmak onu yalnız gizliyordu); ona giden eski kısayolları ve sabitlemeleri de kaldırın. Aynı veri klasöründe eski bir sürüm
     çalıştığı sürece yeni sürüm hiçbir gönderi göndermez. Bu yüzden önce eski sürümü kapatın.
   - **0.1.3–0.1.4'ten:** pencereyi kapatın ve zamanlayıcıdan çıkın (tepsi simgesi → **Çık** ya da Ayarlar → **Durdur**). Unutursanız
     yeni sürüm açılınca ondan kapanmasını ister (gönderdiği bir gönderi varsa bitmesini bekler).
3. Windows: yeni zip'i açın ve `TM Publisher` klasöründeki dosyaları değiştirin. 0.1.3–0.1.4'ten geliyorsanız oradaki
   `TMPublisherScheduler.exe` ve `sqlite3mc.dll` dosyalarını silin; artık kullanılmıyorlar. Tek bir `TMPublisher-<sürüm>-win-x64.exe`
   olan 0.1.0–0.1.2'den geliyorsanız: yeni klasörü istediğiniz yere koyun, oradaki `TMPublisher.exe`'yi başlatın ve eski exe'yi silin.
   Linux: yeni arşivi eski klasörün üzerine açın ve oradaki `tm-publisher-scheduler` ile `libsqlite3mc.so` dosyalarını silin.
4. Pencereyi başlatın. Verileriniz yerinde kalır. Eski otomatik başlatma kaydı (Windows Run anahtarı /
   `~/.config/autostart/tm-publisher.desktop`) kendiliğinden silinir, Görev Zamanlayıcı görevi / systemd zamanlayıcısı yazılır.

## Kaldırma

1. **Ayarlar**'da **TM Publisher kapalıyken de zamanlanmış gönderileri gönder**'i kapatıp **Kaydet**'e basın: Görev Zamanlayıcı
   görevi (Windows) ya da systemd zamanlayıcısı ve hizmeti (Linux) silinir.
2. Pencereyi kapatın.
3. `TM Publisher` klasörünü (Windows) ya da `tm-publisher` klasörünü (Linux) silin.
4. Program zaten silindiyse sistem girdisini elle kaldırın:
   - Windows: **Görev Zamanlayıcı**'yı açın, **Görev Zamanlayıcı Kitaplığı**'nı seçin, `TM Publisher <kod>` görevini silin; ya da
     PowerShell'de: `Get-ScheduledTask -TaskName 'TM Publisher *' | Unregister-ScheduledTask -Confirm:$false`
   - Linux:
     ```sh
     systemctl --user disable --now 'tm-publisher-*.timer'
     rm ~/.config/systemd/user/tm-publisher-*.timer ~/.config/systemd/user/tm-publisher-*.service
     rm -f ~/.config/autostart/tm-publisher-*.desktop ~/.config/autostart/tm-publisher.desktop
     systemctl --user daemon-reload
     ```
5. Verilerinizi de kaldırmak için aşağıdaki veri klasörünü silin. Bu geri alınamaz; gerekebilecekse önce yedek alın.
   Linux'ta anahtarlık kullanıyorsanız anahtarlık uygulamanızdan (Seahorse, KWallet...) "TM Publisher database key" girdisini
   de silin.

## Verileriniz nerede

| | Klasör |
|---|---|
| Windows | `%LOCALAPPDATA%\TMPublisher` |
| Linux | `~/.local/share/tm-publisher` (ya da `$XDG_DATA_HOME/tm-publisher`) |

İçinde: `publisher.db` (şifreli veritabanı), `key.json` (DPAPI, anahtarlık ya da ana parolanızla mühürlenmiş veritabanı
anahtarı), `preferences.json` (yalnız arayüz dili), `logs/` (gün ve tür başına bir dosya: pencere için `app-yyyy-MM-dd.log`, sistem
zamanlayıcısının başlattığı çalıştırmalar için `run-…`, bildirimler için `notify-…`, 0.1.3–0.1.4'ten kalan `scheduler-…`; 7 gün
tutulur; asla anahtar ya da parola içermez), `reports/` (yalnız "Hata bildir" ile sizin yaptığınız günlük zip dosyaları),
`scheduler-instance.lock` (bir veri klasörü için tek gönderici: pencere ya da bir çalıştırma) / `window-instance.lock` (tek pencere),
`system-trigger.json` (sistem zamanlayıcısına ne yazıldığı ve TM Publisher'ı ne zaman başlatacağı), `run-state.json` (çalıştırmaların
art arda hata sayısı ve son kilit bildirimi), `scheduler-paused.txt` (yalnız zamanlama duraklatılmışken) ve `locks/` (bir hesabın
anahtar yenilemesi asla aynı anda iki kez çalışmaz). Medya dosyaları sizin tuttuğunuz yerde kalır; yalnız yolları saklanır.

Başka bir klasör `--data-dir <klasör>` ya da `TMPUBLISHER_DATA_DIR` ortam değişkeniyle kullanılabilir; her veri klasörünün o klasörü
koruyan kendi görevi / zamanlayıcısı olur. Program ilk açılışta yerel kitaplıklarını sistemin geçici klasörüne açar (.NET tek dosya
programlarının olağan davranışı).

Pencere ekran kartıyla değil işlemciyle çizer. Çok büyük bir ekranda çizim yavaş gelirse `--gpu` ile başlatın (ya da
`TMPUBLISHER_GPU=1` ortam değişkenini tanımlayın).

## Hata bildirme

**Hata bildir** düğmesi yan menünün altındadır. Hiçbir şey kendiliğinden gönderilmez:

- **Günlükleri hazırla**, veri klasörünün `reports/` klasöründe `tm-publisher-logs-<tarih>-<saat>.zip` dosyasını yapar ve
  gösterir: son 7 günün günlük dosyaları ve `system.txt` (sürüm, işletim sistemi, mimari, .NET, arayüz dili, çizim kipi, hangi
  yardımcı programların olduğu). Ev klasörünüz, e-posta adresleri ve anahtarlar gizlenir; ayarlar, anahtarlar, veritabanı, hesap
  adları ve gönderi metinleri asla içinde olmaz. En yeni 5 zip tutulur.
- **GitHub'da bildir**, hata formunu tarayıcınızda sürüm ve sistem bilgisi dolu olarak açar; ne olduğunu yazın ve zip dosyasını
  forma sürükleyin.
- **Bilgileri kopyala**, aynı `system.txt` metnini panoya kopyalar.

Günlükler veri klasörünün `logs/` klasöründedir; gün ve program başına bir dosya, Information düzeyi ve üstü. Yalnız son 7 gün
(bugün dahil) tutulur, daha eskileri açılışta ve her gün değişiminde silinir; bir dosya o gün 20 MB'a ulaşınca büyümeyi bırakır.

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
dotnet run --project src/TMPublisher.Desktop     # pencere; penceresiz tek çalıştırma için sonuna -- --run-due ekleyin
```

Sürüm dosyaları yalnız temiz bir çalışma ağacından (program derlendiği commit'i taşır): Windows'ta `pwsh tools/publish.ps1 -Rids
win-x64,linux-x64,linux-arm64`, Linux'ta `tools/publish.sh`; her biri platform başına yerel kitaplıkları içinde, kırpılmış, kendi
kendine yeten tek bir dosya yapar (yerel derleyici gerekmez). Paketler ve `SHA256SUMS.txt` `artifacts/release` altına yazılır (başka bir klasör:
`-Out <klasör>` / `TMPUBLISHER_OUT=<klasör>`). `docs/platforms` altındaki platform rehberleri programın kendi metinlerinden
`python tools/gen-platform-docs.py` ile üretilir.

## Lisans

MIT, bkz. [LICENSE](LICENSE). Üçüncü taraf malzeme: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
