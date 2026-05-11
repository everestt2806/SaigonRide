// SaigonRide Web — small client helpers (live timer, fare poll, station refresh).

(function () {
    'use strict';

    function initLiveTimer() {
        var el = document.querySelector('[data-rental-start-utc]');
        if (!el) return;
        var startUtc = new Date(el.getAttribute('data-rental-start-utc'));
        var rate = parseFloat(el.getAttribute('data-rate-per-min'));
        var timerEl = el.querySelector('[data-timer]');
        var fareEl = el.querySelector('[data-estimated-fare]');

        function tick() {
            var now = new Date();
            var seconds = Math.floor((now - startUtc) / 1000);
            if (seconds < 0) seconds = 0;
            var minutes = Math.floor(seconds / 60);
            var remaining = seconds % 60;
            if (timerEl) {
                timerEl.textContent = ('00' + minutes).slice(-2) + ':' + ('00' + remaining).slice(-2);
            }
            if (fareEl) {
                var billable = Math.max(1, Math.ceil(seconds / 60));
                var estimate = Math.round(billable * rate);
                fareEl.textContent = estimate.toLocaleString() + ' ₫';
            }
        }
        tick();
        setInterval(tick, 1000);
    }

    function initStationAutoRefresh() {
        var container = document.querySelector('[data-station-utilisation-feed]');
        if (!container) return;
        var url = container.getAttribute('data-station-utilisation-feed');
        async function refresh() {
            try {
                var resp = await fetch(url, { headers: { 'Accept': 'application/json' } });
                if (!resp.ok) return;
                var rows = await resp.json();
                rows.forEach(function (row) {
                    var cell = container.querySelector('[data-station="' + row.stationId + '"]');
                    if (!cell) return;
                    cell.querySelector('[data-current]').textContent = row.currentCount;
                    cell.querySelector('[data-occupancy]').textContent = row.occupancyPct + '%';
                });
            } catch (e) { /* swallow */ }
        }
        setInterval(refresh, 30000);
    }

    function initFormValidation() {
        // Map .NET MVC's input-validation-error to Bootstrap's is-invalid
        document.querySelectorAll('.input-validation-error').forEach(function(el) {
            el.classList.add('is-invalid');
        });
        
        // Listen to validation changes from jQuery Unobtrusive Validation if it's present
        if (typeof jQuery !== 'undefined' && jQuery.validator && jQuery.validator.unobtrusive) {
            var originalErrorPlacement = jQuery.validator.defaults.errorPlacement;
            jQuery.validator.setDefaults({
                highlight: function (element) {
                    jQuery(element).closest('.form-control').addClass('is-invalid');
                },
                unhighlight: function (element) {
                    jQuery(element).closest('.form-control').removeClass('is-invalid');
                }
            });
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        initLiveTimer();
        initStationAutoRefresh();
        initFormValidation();
    });
})();
