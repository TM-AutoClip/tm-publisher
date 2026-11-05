# Instagram

[English](instagram.md) · [← README'ye dön](../../README.tr.md#hesaplarınızı-bağlayın)

TM Publisher Instagram ile yalnız **sizin kendi** geliştirici uygulamanız ya da anahtarınızla konuşur. Hiçbir şey bizim bir sunucumuzdan geçmez; öyle bir sunucu yok.

## Neler gönderilebilir

Bu sürümde yalnız video (Reels); açıklama en çok 2.200 karakter ve 5 etiket.

## Adımlar

1. Instagram hesabını profesyonel (İşletme ya da İçerik Üreticisi) hesaba çevirin ve yönettiğiniz bir Facebook Sayfasına bağlayın. <https://help.instagram.com/502981923235522>
2. Meta for Developers'ta bir uygulama oluşturun (kullanım "Other", tür "Business"); App settings → Basic: App ID ve App Secret'ı kopyalayın. <https://developers.facebook.com/apps/creation/>
3. Uygulamaya "Instagram" ürününü ekleyin (Facebook Login ile API kurulumu).
4. Graph API Explorer'da instagram_basic, instagram_content_publish, pages_show_list, pages_read_engagement ve business_management izinli bir kullanıcı anahtarı alın. <https://developers.facebook.com/tools/explorer/>
5. Facebook sorduğunda Sayfayı ve Instagram hesabını seçin, sonra anahtarı kopyalayın.
6. Hepsini buraya yapıştırıp "Bağla ve dene"ye tıklayın.

Deneme başarılı olunca hesap **Hesaplar** altında görünür; gönderilerde ve gruplarda kullanılabilir.

## Bilmeniz gerekenler

- Bu sürümde yalnız video (Reels): Instagram fotoğrafı yalnız herkese açık bir web adresinden alır.
- 24 saatte 100 gönderi, en çok 5 etiket, açıklama en çok 2.200 karakter, video en çok 300 MB.
- Uygulamanız geliştirme modunda kalır. Bir deneme gönderisinin başkalarına göründüğünü bir kez başka bir hesaptan kontrol edin; görünmüyorsa uygulamayı Live'a alın (Meta bir gizlilik politikası adresi isteyebilir).
