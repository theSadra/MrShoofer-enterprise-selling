// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
	'use strict';

	function normalizeDigits(value) {
		if (typeof value !== 'string' || value.length === 0) {
			return value;
		}

		return value
			.replace(/[\u06F0-\u06F9]/g, function (char) {
				return String(char.charCodeAt(0) - 0x06F0);
			})
			.replace(/[\u0660-\u0669]/g, function (char) {
				return String(char.charCodeAt(0) - 0x0660);
			});
	}

	function shouldNormalizeElement(element) {
		if (!element || element.disabled || element.readOnly) {
			return false;
		}

		if (element.getAttribute('data-normalize-digits') === 'false') {
			return false;
		}

		var tagName = (element.tagName || '').toLowerCase();
		if (tagName !== 'input' && tagName !== 'textarea') {
			return false;
		}

		var type = (element.type || '').toLowerCase();
		return type !== 'password' && type !== 'email' && type !== 'url' && type !== 'file';
	}

	function normalizeElementValue(element) {
		if (!shouldNormalizeElement(element)) {
			return;
		}

		var original = element.value;
		var normalized = normalizeDigits(original);
		if (original !== normalized) {
			element.value = normalized;
		}
	}

	document.addEventListener('input', function (event) {
		normalizeElementValue(event.target);
	});

	document.addEventListener('change', function (event) {
		normalizeElementValue(event.target);
	});

	document.addEventListener('submit', function (event) {
		var form = event.target;
		if (!form || !form.elements) {
			return;
		}

		Array.prototype.forEach.call(form.elements, normalizeElementValue);
	});
})();
