/**
 * SALU Examination & Enrollment Portal - Input Masking & Auto-Hyphenation
 * Automatically hyphens CNIC (00000-0000000-0) and Mobile Numbers (0300-0000000)
 */

(function () {
    'use strict';

    /**
     * Format CNIC digits into 00000-0000000-0 format
     * @param {string} value
     * @returns {string}
     */
    function formatCnic(value) {
        if (!value) return '';
        const digits = value.replace(/\D/g, '').slice(0, 13);
        if (digits.length <= 5) {
            return digits;
        } else if (digits.length <= 12) {
            return digits.slice(0, 5) + '-' + digits.slice(5);
        } else {
            return digits.slice(0, 5) + '-' + digits.slice(5, 12) + '-' + digits.slice(12, 13);
        }
    }

    /**
     * Format Mobile digits into 0300-0000000 format
     * @param {string} value
     * @returns {string}
     */
    function formatMobile(value) {
        if (!value) return '';
        let digits = value.replace(/\D/g, '');
        // If international format like 923001234567, normalize to 03001234567
        if (digits.startsWith('923') && digits.length >= 12) {
            digits = '0' + digits.slice(2);
        }
        digits = digits.slice(0, 11);
        if (digits.length <= 4) {
            return digits;
        } else {
            return digits.slice(0, 4) + '-' + digits.slice(4, 11);
        }
    }

    /**
     * Format CNIC or Email (for login field)
     * If user types digits, format as CNIC; if letters or '@' present, leave as email
     * @param {string} value
     * @returns {string}
     */
    function formatCnicOrEmail(value) {
        if (!value) return '';
        const trimmed = value.trim();
        // If it contains '@' or starts with a letter, treated as email/username
        if (trimmed.includes('@') || /^[a-zA-Z]/.test(trimmed)) {
            return value;
        }
        // If it starts with digits or hyphens, treat as CNIC
        const digits = trimmed.replace(/\D/g, '');
        if (digits.length > 0) {
            return formatCnic(trimmed);
        }
        return value;
    }

    /**
     * Handles input formatting with intelligent cursor management
     */
    function handleInputMask(input, maskType, isBackspace) {
        const oldValue = input.value;
        const oldCursor = input.selectionStart || 0;
        
        // Count digits before cursor in previous value
        const digitsBeforeCursor = (oldValue.slice(0, oldCursor).match(/\d/g) || []).length;

        let formatted = '';
        if (maskType === 'cnic') {
            formatted = formatCnic(oldValue);
        } else if (maskType === 'mobile' || maskType === 'phone') {
            formatted = formatMobile(oldValue);
        } else if (maskType === 'cnic-or-email') {
            formatted = formatCnicOrEmail(oldValue);
        }

        if (formatted !== oldValue) {
            input.value = formatted;

            // Recalculate cursor position based on digit count
            let newCursor = 0;
            let digitsEncountered = 0;
            const targetDigits = isBackspace ? Math.max(0, digitsBeforeCursor) : digitsBeforeCursor;

            for (let i = 0; i < formatted.length; i++) {
                if (/\d/.test(formatted[i])) {
                    digitsEncountered++;
                }
                if (digitsEncountered >= targetDigits) {
                    newCursor = i + 1;
                    break;
                }
            }
            if (targetDigits === 0) newCursor = 0;
            if (newCursor > formatted.length) newCursor = formatted.length;

            input.setSelectionRange(newCursor, newCursor);

            // Dispatch input event so Blazor / frameworks receive the updated value
            input.dispatchEvent(new Event('input', { bubbles: true, cancelable: true }));
        }
    }

    /**
     * Determine mask type for a given input element
     */
    function getMaskType(el) {
        if (!el || el.tagName !== 'INPUT') return null;
        const dataMask = el.getAttribute('data-mask');
        if (dataMask) return dataMask.toLowerCase();

        const id = (el.id || '').toLowerCase();
        const name = (el.name || '').toLowerCase();
        const cls = (el.className || '').toLowerCase();

        if (id.includes('cnic') || name.includes('cnic') || cls.includes('salu-cnic')) {
            return 'cnic';
        }
        if (id.includes('phone') || id.includes('mobile') || name.includes('phone') || name.includes('mobile') || cls.includes('salu-phone') || cls.includes('salu-mobile')) {
            return 'mobile';
        }
        if (id.includes('email') && (cls.includes('salu-login') || id.includes('input.email'))) {
            // Login identifier input that accepts both CNIC and Email
            return 'cnic-or-email';
        }
        return null;
    }

    // Global event delegation for seamless real-time formatting across static SSR and interactive pages
    document.addEventListener('input', function (e) {
        const maskType = getMaskType(e.target);
        if (maskType) {
            handleInputMask(e.target, maskType, false);
        }
    }, true);

    document.addEventListener('keydown', function (e) {
        const el = e.target;
        const maskType = getMaskType(el);
        if (!maskType) return;

        // Handle Backspace when immediately preceding a hyphen
        if (e.key === 'Backspace') {
            const cursor = el.selectionStart;
            const endCursor = el.selectionEnd;
            if (cursor === endCursor && cursor > 0 && el.value[cursor - 1] === '-') {
                e.preventDefault();
                // Remove the digit before the hyphen
                const val = el.value;
                const before = val.slice(0, cursor - 2);
                const after = val.slice(cursor);
                el.value = before + after;
                handleInputMask(el, maskType, true);
            }
        }
    }, true);

    document.addEventListener('paste', function (e) {
        const el = e.target;
        const maskType = getMaskType(el);
        if (!maskType) return;

        setTimeout(function () {
            handleInputMask(el, maskType, false);
        }, 0);
    }, true);

    // Global Password Visibility Eye Toggle Handler
    document.addEventListener('click', function (e) {
        const eyeBtn = e.target.closest('.salu-eye-btn, [data-toggle-password], .btn-toggle-password');
        if (!eyeBtn) return;
        e.preventDefault();
        e.stopPropagation();

        // Locate corresponding password input
        const targetSelector = eyeBtn.getAttribute('data-target');
        let input = targetSelector ? document.querySelector(targetSelector) : null;
        if (!input) {
            const container = eyeBtn.closest('.salu-input-box, .input-group, .salu-form-group, .form-floating, div');
            if (container) {
                input = container.querySelector('input[type="password"], input[type="text"]');
            }
        }

        if (!input) return;

        const isPassword = input.type === 'password';
        input.type = isPassword ? 'text' : 'password';

        // Update icon and title tooltip
        const icon = eyeBtn.querySelector('i');
        if (icon) {
            if (isPassword) {
                icon.className = 'bi bi-eye-slash-fill';
                eyeBtn.setAttribute('title', 'Hide password');
                eyeBtn.setAttribute('aria-label', 'Hide password');
            } else {
                icon.className = 'bi bi-eye-fill';
                eyeBtn.setAttribute('title', 'Show password');
                eyeBtn.setAttribute('aria-label', 'Show password');
            }
        }
    }, true);

    // Initial check on load
    function initMasks() {
        document.querySelectorAll('input').forEach(function (input) {
            const maskType = getMaskType(input);
            if (maskType && input.value) {
                handleInputMask(input, maskType, false);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initMasks);
    } else {
        initMasks();
    }
})();

window.saluTheme = window.saluTheme || {
    set: function (theme) {
        const root = document.getElementById('salu-html-root');
        if (root) root.setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }
};
