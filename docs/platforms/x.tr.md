# X

[English](x.md) · [← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)

TM Publisher X ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.

## Neler gönderilebilir

Metin (280 ağırlıklı karakter), en çok 4 fotoğraf ya da bir video. X gönderi başına ücret alır: yaklaşık 0,015 $, bağlantılı 0,20 $.

## Dönüş adresi

Uygulamanıza tam olarak bu adresi kaydedin (bağlantı penceresi kopyalama düğmesiyle gösterir):

```
http://127.0.0.1:47812/callback
```

## Adımlar

1. X geliştirici konsolunu açın ve gönderi atacak X hesabıyla giriş yapın. <https://console.x.com>
2. Bir Project ve App oluşturun. Uygulamanın "User authentication settings" bölümünde OAuth 2.0, tür "Native App", izin "Read and write" seçin.
3. Bu Callback URI'yi tam olarak ekleyin (kopyalayın) ve herhangi bir web sitesi adresi yazıp kaydedin.
4. OAuth 2.0 Client ID'yi kopyalayın (native uygulamanın secret'ı yoktur).
5. X kullandıkça öder: göndermeden önce geliştirici hesabına kredi yükleyin. <https://docs.x.com/x-api/getting-started/pricing>
6. Client ID'yi yapıştırıp "Bağla ve dene"ye tıklayın; tarayıcıda izin verin.

Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.

## Bilmeniz gerekenler

- X gönderi başına ücret alır (yaklaşık 0,015 $; bağlantılı gönderi yaklaşık 0,20 $). Yazma ekranı göndermeden önce tahmini gösterir.
