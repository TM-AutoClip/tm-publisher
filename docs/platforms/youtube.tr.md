# YouTube

[English](youtube.md) · [← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)

TM Publisher YouTube ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.

## Neler gönderilebilir

Gönderi başına bir video (normal ya da Shorts): başlık, açıklama ve etiketlerle. Başka görünürlük seçmezseniz gizli yüklenir.

## Dönüş adresi

Uygulamanıza tam olarak bu adresi kaydedin (bağlantı penceresi kopyalama düğmesiyle gösterir):

```
http://127.0.0.1:47811/callback
```

## Adımlar

1. Google Cloud'da bir proje oluşturun (ücretsiz Google hesabı yeter). <https://console.cloud.google.com/projectcreate>
2. O projede "YouTube Data API v3"ü etkinleştirin. <https://console.cloud.google.com/apis/library/youtube.googleapis.com>
3. Google Auth Platform → Branding: uygulamaya bir ad ve e-posta adresinizi verin. <https://console.cloud.google.com/auth/branding>
4. Audience: "External" seçin ve "Publish app"e tıklayın, durum "In production" olsun ("Testing"te Google erişimi 7 gün sonra düşürür). <https://console.cloud.google.com/auth/audience>
5. Clients → Create client → Application type "Desktop app"; Client ID ve Client secret'ı kopyalayın. <https://console.cloud.google.com/auth/clients>
6. Buraya yapıştırıp "Bağla ve dene"ye tıklayın; tarayıcıda Google girişi açılır.
7. Kanalın hesabını seçin. Doğrulanmamış kendi uygulamanız için Google "Google bu uygulamayı doğrulamadı" der: "Gelişmiş" → "… adresine git (güvenli değil)"e tıklayın — uygulama sizin.

Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.

## Bilmeniz gerekenler

- YouTube, denetlenmemiş bir projeden yüklenen videoları gizli tutabilir; öyleyse Google Cloud projesinde API denetimine başvurun ya da görünürlüğü YouTube Studio'dan değiştirin.
- Proje başına günde yaklaşık 100 yükleme; kota Pasifik saatiyle gece yarısı sıfırlanır.
- Doğrulanmamış bir uygulamayı en çok 100 Google hesabı kullanabilir: size yeter.
