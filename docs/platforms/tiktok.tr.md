# TikTok

[English](tiktok.md) · [← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)

TM Publisher TikTok ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.

## Neler gönderilebilir

Gönderi başına bir video. Varsayılan olarak TikTok gelen kutunuza taslak gider; isteğe bağlı doğrudan paylaşım her zaman gizlidir.

## Dönüş adresi

Uygulamanıza tam olarak bu adresi kaydedin (bağlantı penceresi kopyalama düğmesiyle gösterir):

```
http://127.0.0.1:47813/callback
```

## Adımlar

1. TikTok for Developers'a girip bir uygulama oluşturun (Manage apps → Connect an app). <https://developers.tiktok.com/apps/>
2. "Login Kit" (platform: Desktop) ve "Content Posting API" ürünlerini ekleyin; gizli doğrudan seçeneği istiyorsanız "Direct Post"u açın.
3. Login Kit'te bu dönüş adresini tam olarak ekleyin (kopyalayın).
4. user.info.basic, video.upload ve video.publish izinlerini ekleyin; sandbox'ta TikTok hesabınızı hedef kullanıcı olarak ekleyin.
5. Client key ve Client secret'ı kopyalayın.
6. Buraya yapıştırıp "Bağla ve dene"ye tıklayın; tarayıcıda izin verin.

Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.

## Bilmeniz gerekenler

- Varsayılan olarak video TikTok gelen kutunuza taslak gider; gönderiyi (açıklama, görünürlük) TikTok uygulamasında tamamlarsınız. Taslak metin taşımaz.
- Doğrudan paylaşım isteğe bağlıdır ve TikTok'un denetlemediği bir uygulamada her zaman gizlidir (yalnız siz görürsünüz).
- TikTok denetimi "kendi hesaplarıma yükleme aracı"nı kabul etmez, bu yüzden herkese açık doğrudan paylaşım kapalı kalır; 24 saatte en çok 5 taslak bekleyebilir.
