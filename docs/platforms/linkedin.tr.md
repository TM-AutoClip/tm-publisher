# LinkedIn

[English](linkedin.md) · [← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)

TM Publisher LinkedIn ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.

## Neler gönderilebilir

Kişisel profilinizde metin, fotoğraf ya da bir video.

## Adımlar

1. LinkedIn Developers'ta bir uygulama oluşturun (uygulamanın şirketi olarak bir LinkedIn Sayfası gerekir; kendi sayfanız olur). <https://www.linkedin.com/developers/apps/new>
2. Products: "Share on LinkedIn" ve "Sign In with LinkedIn using OpenID Connect"i isteyin (ikisi de hemen verilir).
3. OAuth token generator'ı açın, uygulamanızı ve openid, profile, w_member_social izinlerini seçip anahtarı isteyin. <https://www.linkedin.com/developers/tools/oauth/token-generator>
4. Erişim anahtarını kopyalayıp buraya yapıştırın.
5. "Bağla ve dene"ye tıklayın.

Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.

## Bilmeniz gerekenler

- Anahtar 60 gün sürer ve LinkedIn kişisel uygulamaların kendiliğinden yenilemesine izin vermez: 60 günde bir yeniden bağlayın (hesap listesi bir hafta önce hatırlatır).
