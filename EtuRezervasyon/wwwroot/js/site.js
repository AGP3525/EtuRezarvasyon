console.log("Site.js yüklendi");
document.addEventListener('DOMContentLoaded', function() {
    const loginForm = document.getElementById('loginForm');
    
    if (loginForm) {
        loginForm.addEventListener('submit', function(e) {
            e.preventDefault();
            
            // Form verilerini al
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            
            // Burada form gönderme işlemini yapabilirsiniz
            // Örnek olarak formu normal şekilde submit edelim
            this.submit();
        });
    }
    
    // Hakkımızda ve İletişim bağlantılarına tıklandığında sayfayı aşağı kaydır
    const aboutLinks = document.querySelectorAll('a[href="#about-section"]');
    
    aboutLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            const aboutSection = document.getElementById('about-section');
            if (aboutSection) {
                aboutSection.scrollIntoView({ behavior: 'smooth' });
            }
        });
    });
});