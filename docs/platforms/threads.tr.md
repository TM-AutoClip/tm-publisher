# Threads

[English](threads.md) · [← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)

TM Publisher Threads ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.

## Neler gönderilebilir

Bu sürümde yalnız metin (500 karakter).

## Dönüş adresi

Uygulamanıza tam olarak bu adresi kaydedin (bağlantı penceresi kopyalama düğmesiyle gösterir):

```
https://localhost/callback
```

## Adımlar

1. Meta for Developers'ta "Access the Threads API" kullanımlı bir uygulama oluşturun. <https://developers.facebook.com/apps/creation/>
2. App roles → Roles → Threads hesabınızı "Threads Tester" olarak ekleyin, sonra daveti Threads'te kabul edin (Ayarlar → Hesap → Web sitesi izinleri).
3. Use cases → Threads API → Settings: bu dönüş adresini "Redirect Callback URLs"e ekleyin (kopyalayın) ve kaydedin.
4. Aynı sayfadan Threads App ID ve Threads App Secret'ı kopyalayın.
5. Buraya yapıştırıp "Bağla ve dene"ye tıklayın; tarayıcıda izin verin.
6. Tarayıcı ardından açılmayan bir adrese gider: adres çubuğundaki adresin tamamını kopyalayıp buradaki kutuya yapıştırın.

Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.

## Bilmeniz gerekenler

- Bu sürümde yalnız metin: Threads fotoğraf ve videoyu yalnız herkese açık bir web adresinden alır.
- Erişim 60 gün sürer ve TM Publisher kullanıldıkça yeniler; program bu kadar süre kullanılmazsa yeniden bağlayın.
