document.addEventListener('DOMContentLoaded', function() {

    // --- 1. محاسبه‌گر قیمت گلد ---
    // ----------------------------
    const goldAmountInput = document.getElementById('gold-amount');
    const finalPriceDisplay = document.getElementById('final-price');
    const buyButton = document.getElementById('buy-calculated-gold');
    const pricePerGold = 1500;

    // --- انتخاب المان‌های مدال ---
    const modal = document.getElementById('payment-unavailable-modal');
    const closeModalBtn = document.getElementById('close-modal-btn');
    const modalOverlay = modal ? modal.querySelector('.modal-overlay') : null; // پیدا کردن overlay درون مدال

    // بررسی وجود المان‌های محاسبه‌گر
    if (goldAmountInput && finalPriceDisplay && buyButton) {

        goldAmountInput.addEventListener('input', function() {
            let amount = parseInt(goldAmountInput.value);
            if (isNaN(amount) || amount <= 0) {
                amount = 0;
                finalPriceDisplay.textContent = '۰ تومان';
                buyButton.disabled = true;
                buyButton.classList.add('disabled');
            } else {
                const totalPrice = amount * pricePerGold;
                finalPriceDisplay.textContent = totalPrice.toLocaleString('fa-IR') + ' تومان';
                buyButton.disabled = false;
                buyButton.classList.remove('disabled');
            }
        });

        // --- تغییر رفتار دکمه خرید محاسبه‌گر ---
        buyButton.addEventListener('click', function(e) {
            // جلوگیری از هرگونه رفتار پیش‌فرض دکمه (اگر داخل فرم باشد)
            e.preventDefault();

            // فقط اگر دکمه غیرفعال نباشد، مدال را نمایش بده
            if (!buyButton.disabled && modal) {
                modal.classList.add('show'); // نمایش مدال با افزودن کلاس show
            }
        });

    } else {
        console.warn("برخی از المان‌های محاسبه‌گر قیمت یافت نشدند.");
    }

    // --- مدیریت بستن مدال ---
    // بررسی وجود المان‌های مدال قبل از افزودن listener
    if (modal && closeModalBtn && modalOverlay) {
        // تابع برای بستن مدال
        const closeModal = () => {
            modal.classList.remove('show'); // مخفی کردن مدال با حذف کلاس show
        };

        // بستن مدال با کلیک روی دکمه بستن (×)
        closeModalBtn.addEventListener('click', closeModal);

        // بستن مدال با کلیک روی پس‌زمینه تیره (overlay)
        modalOverlay.addEventListener('click', closeModal);

        // (اختیاری) بستن مدال با زدن دکمه Escape روی کیبورد
        document.addEventListener('keydown', function(event) {
            if (event.key === "Escape" && modal.classList.contains('show')) {
                closeModal();
            }
        });

    } else {
         console.warn("المان‌های مدال (modal, close button, or overlay) یافت نشدند.");
    }


    // --- 2. اسکرول نرم برای لینک‌های منو (بدون تغییر نسبت به قبل) ---
    // -----------------------------------------------------------
    const navLinks = document.querySelectorAll('#header nav ul li a[href^="#"]');
    navLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            const targetId = this.getAttribute('href');
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                const header = document.getElementById('header');
                const headerOffset = header ? header.offsetHeight : 0;
                const elementPosition = targetElement.getBoundingClientRect().top + window.pageYOffset;
                const offsetPosition = elementPosition - headerOffset;
                window.scrollTo({
                    top: offsetPosition,
                    behavior: "smooth"
                });
                // (اختیاری) فعال کردن لینک منو
                 navLinks.forEach(nav => nav.classList.remove('active'));
                 this.classList.add('active');
            }
        });
    });

    // (اختیاری) هایلایت کردن لینک منو هنگام اسکرول (بدون تغییر نسبت به قبل)
    window.addEventListener('scroll', function() {
       let currentSection = '';
       const sections = document.querySelectorAll('section[id]');
       const headerHeight = document.getElementById('header') ? document.getElementById('header').offsetHeight : 0;
       const scrollPosition = window.pageYOffset;

       sections.forEach( section => {
           const sectionTop = section.offsetTop - headerHeight - 50;
           const sectionHeight = section.offsetHeight;
           if (scrollPosition >= sectionTop && scrollPosition < sectionTop + sectionHeight) {
               currentSection = section.getAttribute('id');
           }
       });

       if (!currentSection && scrollPosition < window.innerHeight / 2) {
            currentSection = 'hero';
       }

       navLinks.forEach( link => {
           link.classList.remove('active');
           if (link.getAttribute('href') === `#${currentSection}`) {
               link.classList.add('active');
           }
       });

        if ((window.innerHeight + window.scrollY) >= document.body.offsetHeight - 50) {
            navLinks.forEach(link => link.classList.remove('active'));
            const contactLink = document.querySelector('nav ul li a[href="#contact"]');
            if (contactLink) contactLink.classList.add('active');
        }
    });


}); // پایان DOMContentLoaded
