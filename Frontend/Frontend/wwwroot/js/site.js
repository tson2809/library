// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
window.normalizeSearchText = function (value) {
	const raw = (value || '').toString().toLowerCase().trim();
	if (!raw) {
		return '';
	}

	try {
		return raw
			.normalize('NFD')
			.replace(/[\u0300-\u036f]/g, '')
			.replace(/đ/g, 'd');
	} catch {
		return raw.replace(/đ/g, 'd');
	}
};

window.renderPagination = function (options) {
	if (!options || !(options.pager instanceof HTMLElement)) {
		return;
	}

	const pager = options.pager;
	const totalPages = Number.isInteger(options.totalPages) ? options.totalPages : 1;
	const currentPage = Number.isInteger(options.currentPage) ? options.currentPage : 1;
	const onPageChange = typeof options.onPageChange === 'function' ? options.onPageChange : null;
	const showPrevNext = options.showPrevNext !== false;

	pager.innerHTML = '';
	if (totalPages <= 1 || !onPageChange) {
		return;
	}

	const createButton = function (text, page, isActive, isDisabled) {
		const button = document.createElement('button');
		button.type = 'button';
		button.className = 'pager-btn' + (isActive ? ' active' : '');
		button.textContent = text;
		button.disabled = !!isDisabled;
		button.addEventListener('click', function () {
			onPageChange(page);
		});
		return button;
	};

	if (showPrevNext) {
		pager.appendChild(createButton('‹ Trước', Math.max(1, currentPage - 1), false, currentPage === 1));
	}

	for (let page = 1; page <= totalPages; page += 1) {
		pager.appendChild(createButton(page.toString(), page, page === currentPage, false));
	}

	if (showPrevNext) {
		pager.appendChild(createButton('Sau ›', Math.min(totalPages, currentPage + 1), false, currentPage === totalPages));
	}
};

(() => {
	const debounce = (fn, delay) => {
		let timer = null;
		return (...args) => {
			if (timer) {
				clearTimeout(timer);
			}
			timer = setTimeout(() => fn(...args), delay);
		};
	};

	document.addEventListener('DOMContentLoaded', () => {
		const wireRealtimeFilter = (inputId, buttonId) => {
			const input = document.getElementById(inputId);
			const button = document.getElementById(buttonId);
			if (!(input instanceof HTMLInputElement) || !(button instanceof HTMLButtonElement)) {
				return;
			}

			const trigger = debounce(() => button.click(), 180);
			input.addEventListener('input', trigger);
		};

		wireRealtimeFilter('searchInput', 'applyCatalogFilter');
		wireRealtimeFilter('loanSearchInput', 'applyLoanFilter');

		document.addEventListener('keydown', (event) => {
			const target = event.target;
			if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement) {
				if (event.key !== '/' || event.altKey || event.ctrlKey || event.metaKey) {
					return;
				}
			}

			if (!event.ctrlKey && !event.metaKey && !event.altKey && event.key === '/') {
				const focusTarget = document.getElementById('loanSearchInput') || document.getElementById('searchInput');
				if (focusTarget instanceof HTMLInputElement) {
					event.preventDefault();
					focusTarget.focus();
					focusTarget.select();
				}
			}

			if (event.altKey && !event.ctrlKey && !event.metaKey && event.key.toLowerCase() === 'n') {
				const trigger = document.querySelector('[data-bs-target="#createLoanModal"]') || document.getElementById('openCreateBookModalBtn');
				if (trigger instanceof HTMLElement) {
					event.preventDefault();
					trigger.click();
				}
			}
		});
	});
})();
