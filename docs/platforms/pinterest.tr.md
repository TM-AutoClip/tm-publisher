# Pinterest

[English](pinterest.md) · [← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)

TM Publisher Pinterest ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.

## Neler gönderilebilir

Seçtiğiniz panoya başlık, açıklama ve isteğe bağlı bağlantıyla bir resim ya da bir video pini.

## Dönüş adresi

Uygulamanıza tam olarak bu adresi kaydedin (bağlantı penceresi kopyalama düğmesiyle gösterir):

```
https://localhost/callback
```

## Adımlar

1. Pinterest Developers'ta uygulamanızı bağlayın (işletme hesabı önerilir). <https://developers.pinterest.com/apps/>
2. Bu dönüş adresini (kopyalayın) uygulamanın redirect URI'lerine ekleyin. Herhangi bir https adresi olur; sayfanın açılması gerekmez.
3. App id ve App secret key'i kopyalayın.
4. Buraya yapıştırın, isterseniz panoyu yazın ve "Bağla ve dene"ye tıklayın.
5. İzin verdikten sonra tarayıcı açılmayan bir adrese gider: adres çubuğundan kopyalayıp buradaki kutuya yapıştırın.

Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.

## Bilmeniz gerekenler

- Deneme (Trial) erişiminde pinleriniz yalnız size görünür. Standart erişim için uygulamanın girişini gösteren kısa bir videoyu Pinterest'in incelemesine göndermek gerekir.
- 14 Eylül 2026 ve sonrasında açılan uygulamalar kişisel (işletme olmayan) hesaplarda "403" alabilir; işletme hesabı kullanın.
