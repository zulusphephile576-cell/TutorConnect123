/* TutorConnect client script: no framework, progressive enhancement only. */
(function () {
    "use strict";

    var body = document.body;

    function $(sel, root) { return (root || document).querySelector(sel); }
    function $all(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

    function token() {
        var input = $("#af-token input[name=__RequestVerificationToken]");
        return input ? input.value : "";
    }

    function post(url, data) {
        var form = new URLSearchParams();
        Object.keys(data || {}).forEach(function (k) { form.append(k, data[k]); });
        form.append("__RequestVerificationToken", token());
        return fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded", "X-Requested-With": "XMLHttpRequest" },
            body: form.toString()
        }).then(function (r) {
            return r.json().catch(function () { return {}; }).then(function (json) {
                if (!r.ok) throw new Error(json.message || (r.status === 401 ? "Your session expired. Please sign in again." : "Request failed."));
                return json;
            });
        });
    }

    function getJson(url) {
        return fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); });
    }

    function escapeHtml(s) {
        return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
    }

    /* ---------- Toasts ---------- */
    function toast(title, text, type, sticky) {
        var stack = $("#toasts");
        if (!stack) return;
        var el = document.createElement("div");
        el.className = "toast " + (type || "");
        el.setAttribute("role", "status");
        var icon = type === "error" ? "fa-circle-exclamation" : type === "success" ? "fa-circle-check" : "fa-bell";
        el.innerHTML = '<i class="fa-solid ' + icon + ' toast-icon" aria-hidden="true"></i>' +
            '<div><div class="toast-title">' + escapeHtml(title) + '</div>' +
            (text ? '<div class="toast-text">' + escapeHtml(text) + '</div>' : "") + '</div>' +
            '<button type="button" aria-label="Dismiss"><i class="fa-solid fa-xmark"></i></button>';
        el.querySelector("button").addEventListener("click", function () { el.remove(); });
        stack.appendChild(el);
        if (!sticky) setTimeout(function () { el.remove(); }, 6000);
    }
    window.TC = { toast: toast };

    /* ---------- Sidebar (mobile) ---------- */
    $all("[data-nav-toggle]").forEach(function (btn) {
        btn.addEventListener("click", function () {
            var open = body.classList.toggle("nav-open");
            btn.setAttribute("aria-expanded", open ? "true" : "false");
        });
    });
    var scrim = $(".scrim");
    if (scrim) scrim.addEventListener("click", function () { body.classList.remove("nav-open"); });

    /* ---------- Flash dismiss ---------- */
    $all(".alert .close").forEach(function (b) {
        b.addEventListener("click", function () { b.closest(".alert").remove(); });
    });

    /* ---------- Confirm before destructive submits ---------- */
    document.addEventListener("submit", function (e) {
        var msg = e.target.getAttribute("data-confirm");
        if (msg && !window.confirm(msg)) e.preventDefault();
    }, true);

    /* ---------- Stop double submits (double-clicking Pay or Approve used to send two requests) ---------- */
    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (e.defaultPrevented || form.method.toLowerCase() !== "post") return;
        if (form.getAttribute("data-submitting")) { e.preventDefault(); return; }
        form.setAttribute("data-submitting", "1");
        $all("button[type=submit], button:not([type])", form).forEach(function (b) {
            b.classList.add("is-busy");
            setTimeout(function () { b.disabled = true; }, 0);
        });
    });
    // Coming back with the browser's Back button restores the page from cache: re-enable its forms.
    window.addEventListener("pageshow", function () {
        $all("form[data-submitting]").forEach(function (f) {
            f.removeAttribute("data-submitting");
            $all("button", f).forEach(function (b) { b.disabled = false; b.classList.remove("is-busy"); });
        });
    });

    /* ---------- Theme toggle ---------- */
    var themeBtn = $("#themeToggle");
    if (themeBtn) {
        themeBtn.addEventListener("click", function () {
            var dark = body.classList.toggle("dark");
            themeBtn.innerHTML = '<i class="fa-solid ' + (dark ? "fa-sun" : "fa-moon") + '"></i>';
            post(themeBtn.getAttribute("data-url"), {}).catch(function () {
                body.classList.toggle("dark");
                toast("Couldn't save your theme", "Try again from Settings.", "error");
            });
        });
    }

    /* ---------- Filtering: tables and card lists ----------
       <div data-filter-table="#target"> holds a search box and selects with data-filter-attr="x".
       Rows are "tbody tr" (tables) or [data-filter-item] (cards). A row matches a select when its data-x
       equals the value, or when data-x is a comma list that contains it (e.g. a module in several courses).
       A select with data-depends-on="#other" only shows options whose data-parent matches #other's value. */
    $all("[data-filter-table]").forEach(function (scope) {
        var target = $(scope.getAttribute("data-filter-table"));
        if (!target) return;
        var text = $("input[type=search]", scope);
        var selects = $all("select[data-filter-attr]", scope);
        var counter = $("[data-filter-count]", scope);
        var empty = $(".no-results", target) || $(".no-results", target.parentNode);
        var rows = function () {
            var items = $all("[data-filter-item]", target);
            return items.length ? items : $all("tbody tr", target).filter(function (r) { return !r.classList.contains("no-results"); });
        };
        var matches = function (row, attr, value) {
            var have = row.getAttribute("data-" + attr) || "";
            return have === value || have.split(",").indexOf(value) !== -1;
        };

        // dependent dropdowns (faculty -> department -> course)
        selects.forEach(function (s) {
            var dep = s.getAttribute("data-depends-on");
            if (!dep) return;
            var parent = $(dep, scope) || $(dep);
            if (!parent) return;
            var sync = function () {
                var pv = parent.value;
                $all("option[data-parent]", s).forEach(function (o) { o.hidden = !!pv && o.getAttribute("data-parent").split(",").indexOf(pv) === -1; });
                var cur = s.options[s.selectedIndex];
                if (cur && cur.hidden) { s.value = ""; s.dispatchEvent(new Event("change")); }
            };
            parent.addEventListener("change", sync);
            sync();
        });

        function apply() {
            var q = text ? text.value.trim().toLowerCase() : "";
            var shown = 0, all = rows();
            all.forEach(function (row) {
                var ok = !q || row.textContent.toLowerCase().indexOf(q) !== -1;
                selects.forEach(function (s) { if (s.value && !matches(row, s.getAttribute("data-filter-attr"), s.value)) ok = false; });
                row.style.display = ok ? "" : "none";
                if (ok) shown++;
            });
            // hide group headings whose rows are all hidden
            $all("[data-filter-group]", target).forEach(function (g) {
                var any = $all("[data-filter-item]", g).some(function (r) { return r.style.display !== "none"; });
                g.style.display = any ? "" : "none";
            });
            if (empty) empty.style.display = shown === 0 ? (empty.tagName === "TR" ? "table-row" : "block") : "none";
            if (counter) counter.textContent = shown + " of " + all.length;
        }
        if (text) text.addEventListener("input", apply);
        selects.forEach(function (s) { s.addEventListener("change", apply); });
        apply();
    });

    /* ---------- Attendance marking (AJAX, saved instantly) ---------- */
    function updateTally(root) {
        var counts = { Present: 0, Absent: 0, None: 0 };
        $all(".att-buttons", root).forEach(function (g) {
            var on = $("button.on", g);
            counts[on ? on.getAttribute("data-status") : "None"]++;
        });
        Object.keys(counts).forEach(function (k) {
            var el = $("[data-tally=" + k + "]");
            if (el) el.textContent = counts[k];
        });
    }

    var sheet = $("[data-register-sheet]");
    if (sheet) {
        sheet.addEventListener("click", function (e) {
            var btn = e.target.closest(".att-buttons button");
            if (!btn) return;
            var group = btn.parentNode;
            var status = btn.getAttribute("data-status");
            var payload = {};
            payload[sheet.getAttribute("data-key")] = sheet.getAttribute("data-id");
            payload.studentId = group.getAttribute("data-student");
            payload.status = status;
            group.classList.add("saving");
            post(sheet.getAttribute("data-url"), payload).then(function () {
                $all("button", group).forEach(function (b) {
                    var on = b === btn;
                    b.classList.toggle("on", on);
                    b.setAttribute("aria-pressed", on ? "true" : "false");
                });
                var method = $("[data-method-for='" + payload.studentId + "']");
                if (method) method.textContent = "";
                updateTally(sheet);
            }).catch(function (err) {
                toast("Not saved", err.message, "error");
            }).then(function () { group.classList.remove("saving"); });
        });
        updateTally(sheet);
    }

    /* ---------- Live counts (QR screen + register) ---------- */
    var live = $("[data-live-url]");
    if (live) {
        var liveUrl = live.getAttribute("data-live-url");
        var refresh = function () {
            getJson(liveUrl).then(function (d) {
                ["enrolled", "present", "late", "absent"].forEach(function (k) {
                    $all("[data-live=" + k + "]").forEach(function (el) { el.textContent = d[k]; });
                });
                var checked = $("[data-live=checkedin]");
                if (checked) checked.textContent = d.present + (d.late || 0);
                var bar = $("[data-live=bar]");
                if (bar) bar.style.width = (d.enrolled ? Math.round(100 * (d.present + (d.late || 0)) / d.enrolled) : 0) + "%";
                if (sheet) {
                    d.students.forEach(function (s) {
                        var group = $(".att-buttons[data-student='" + s.id + "']", sheet);
                        if (!group || group.classList.contains("saving")) return;
                        $all("button", group).forEach(function (b) {
                            var on = b.getAttribute("data-status") === s.status;
                            b.classList.toggle("on", on);
                            b.setAttribute("aria-pressed", on ? "true" : "false");
                        });
                        var method = $("[data-method-for='" + s.id + "']");
                        if (method) method.textContent = s.method === "QR" ? "Scanned QR" : "";
                    });
                    updateTally(sheet);
                }
            }).catch(function () { /* keep trying quietly */ });
        };
        setInterval(refresh, 5000);
    }

    /* ---------- QR code (generated in the browser by the bundled qrcode-generator, no internet needed) ---------- */
    var qr = $("#qr-image");
    if (qr && window.qrcode) {
        var code = window.qrcode(0, "M");
        code.addData(qr.getAttribute("data-url"));
        code.make();
        qr.innerHTML = code.createSvgTag({ cellSize: 8, margin: 2, scalable: true });
    }
    $all("[data-fullscreen]").forEach(function (b) {
        b.addEventListener("click", function () {
            var el = $(b.getAttribute("data-fullscreen"));
            var box = el && el.closest(".qr-box");
            if (box && box.requestFullscreen) box.requestFullscreen();
        });
    });
    $all("[data-copy]").forEach(function (b) {
        b.addEventListener("click", function () {
            var text = b.getAttribute("data-copy");
            if (navigator.clipboard) navigator.clipboard.writeText(text).then(function () { toast("Link copied", null, "success"); });
        });
    });

    /* ---------- Booking price calculator ---------- */
    var bookForm = $("[data-book-form]");
    if (bookForm) {
        var rate = parseFloat(bookForm.getAttribute("data-rate")) || 0;
        var hours = $("#Hours", bookForm);
        var totalEl = $("[data-total]");
        var hoursEl = $("[data-hours-label]");
        var slotEl = $("[data-slot-label]");
        var maxNote = $("[data-max-note]");
        // South African Rand: R1 050,00 (non-breaking space for thousands, comma for cents)
        var fmt = function (n) { var p = n.toFixed(2).split("."); return "R" + p[0].replace(/\B(?=(\d{3})+(?!\d))/g, "\u00A0") + "," + p[1]; };

        var recalc = function () {
            var slot = $("input[name=SlotId]:checked", bookForm);
            var max = slot ? parseInt(slot.getAttribute("data-max"), 10) : 8;
            hours.max = max;
            if (parseInt(hours.value, 10) > max) hours.value = max;
            if (parseInt(hours.value, 10) < 1 || !hours.value) hours.value = 1;
            var h = parseInt(hours.value, 10);
            if (totalEl) totalEl.textContent = fmt(h * rate);
            if (hoursEl) hoursEl.textContent = h + (h === 1 ? " hour" : " hours") + " × " + fmt(rate);
            if (slotEl) slotEl.textContent = slot ? slot.getAttribute("data-label") : "Choose a slot";
            if (maxNote) maxNote.textContent = slot ? "This slot allows up to " + max + (max === 1 ? " hour." : " hours.") : "";
        };
        bookForm.addEventListener("change", recalc);
        hours.addEventListener("input", recalc);
        $all("[data-step]", bookForm).forEach(function (b) {
            b.addEventListener("click", function () {
                hours.value = (parseInt(hours.value, 10) || 1) + parseInt(b.getAttribute("data-step"), 10);
                recalc();
            });
        });
        recalc();
    }

    /* ---------- Payment status polling (waiting for ITN) ---------- */
    var waiting = $("[data-payment-status-url]");
    if (waiting) {
        var tries = 0;
        var poll = setInterval(function () {
            tries++;
            getJson(waiting.getAttribute("data-payment-status-url")).then(function (d) {
                if (d.status !== "Pending" || tries > 40) { clearInterval(poll); window.location.reload(); }
            }).catch(function () { });
        }, 3000);
    }

    /* ---------- 20-minute reminders ---------- */
    var reminderUrl = body.getAttribute("data-reminders-url");
    if (reminderUrl) {
        var seenKey = "tc-reminders-seen";
        var seen = {};
        try { seen = JSON.parse(sessionStorage.getItem(seenKey) || "{}"); } catch (e) { seen = {}; }

        var check = function () {
            getJson(reminderUrl).then(function (items) {
                items.forEach(function (r) {
                    if (seen[r.key]) return;
                    seen[r.key] = true;
                    toast(r.minutes <= 1 ? "Starting now" : "Starts in " + r.minutes + " minutes", r.title + " at " + r.time, "", true);
                });
                try { sessionStorage.setItem(seenKey, JSON.stringify(seen)); } catch (e) { }
            }).catch(function () { });
        };
        check();
        setInterval(check, 60000);
    }

    /* ---------- Course form: add module rows progressively ---------- */
    var rows = $all("[data-module-row]");
    var addRow = $("[data-add-row]");
    if (rows.length && addRow) {
        var visible = function () { return rows.filter(function (r) { return !r.hidden; }).length; };
        rows.forEach(function (r, i) {
            var filled = $all("input", r).some(function (inp) { return inp.type !== "number" && inp.value; });
            if (i > 1 && !filled) r.hidden = true;
        });
        var sync = function () { addRow.hidden = visible() >= rows.length; };
        addRow.addEventListener("click", function () {
            var next = rows.filter(function (r) { return r.hidden; })[0];
            if (next) { next.hidden = false; $("input", next).focus(); }
            sync();
        });
        sync();
    }

    /* ---------- Application form: module list follows the chosen tutor year ---------- */
    var modSelect = $("[data-module-select]");
    var yearSelect = $("[data-year-select]");
    if (modSelect && yearSelect) {
        var filterModules = function () {
            var y = yearSelect.value;
            $all("option[data-year]", modSelect).forEach(function (o) { o.hidden = !!y && o.getAttribute("data-year") !== y; });
            var sel = modSelect.options[modSelect.selectedIndex];
            if (sel && sel.hidden) modSelect.value = "";
        };
        yearSelect.addEventListener("change", filterModules);
        filterModules();
    }

    /* ---------- Dropdown menus (user menu, notifications) ---------- */
    function closeMenus(except) {
        $all(".menu").forEach(function (m) {
            if (m === except) return;
            var panel = $(".menu-panel", m), btn = $("[data-menu-button]", m);
            if (panel && !panel.hidden) { panel.hidden = true; btn.setAttribute("aria-expanded", "false"); }
        });
    }
    $all(".menu").forEach(function (menu) {
        var btn = $("[data-menu-button]", menu), panel = $(".menu-panel", menu);
        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            var open = panel.hidden;
            closeMenus(menu);
            panel.hidden = !open;
            btn.setAttribute("aria-expanded", open ? "true" : "false");
            if (open) menu.dispatchEvent(new CustomEvent("menu:open"));
        });
        panel.addEventListener("click", function (e) { e.stopPropagation(); });
    });
    document.addEventListener("click", function () { closeMenus(); });
    document.addEventListener("keydown", function (e) { if (e.key === "Escape") closeMenus(); });

    /* ---------- Notification centre ---------- */
    var notif = $("[data-notifications]");
    if (notif) {
        var countEl = $(".bell-count", notif), listEl = $("[data-notif-list]", notif), statusEl = $("[data-notif-status]", notif);
        var setCount = function (n) { countEl.hidden = !n; countEl.textContent = n > 9 ? "9+" : n; };
        var load = function (render) {
            return getJson(notif.getAttribute("data-url")).then(function (d) {
                setCount(d.unread);
                if (!render) return d;
                statusEl.textContent = d.unread ? d.unread + " new" : "All caught up";
                listEl.innerHTML = d.items.length ? d.items.map(function (i) {
                    // each notification opens its page (or its details when it has none)
                    return '<li><a class="notif-item' + (i.unread ? " is-new" : "") + '" href="' + escapeHtml(i.url || "#") + '"><span class="notif-dot"></span><div><div>' +
                        escapeHtml(i.message) + '</div><div class="notif-time">' + escapeHtml(i.ago) + '</div></div><i class="fa-solid fa-chevron-right notif-go" aria-hidden="true"></i></a></li>';
                }).join("") : '<li class="notif-empty">Nothing yet. Bookings, tutorials and decisions will show up here.</li>';
                return d;
            });
        };
        load(false).catch(function () { });
        setInterval(function () { load(false).catch(function () { }); }, 60000);
        notif.addEventListener("menu:open", function () {
            load(true).then(function (d) {
                if (d.unread) post(notif.getAttribute("data-seen-url"), {}).then(function () { setCount(0); }).catch(function () { });
            }).catch(function () { listEl.innerHTML = '<li class="notif-empty">Couldn\'t load notifications.</li>'; });
        });
    }

    /* ---------- Command palette (Ctrl+K) ---------- */
    var palette = $("#palette");
    if (palette) {
        var input = $("input", palette), list = $(".palette-list", palette), items = [], active = 0;
        var entries = $all(".sidebar .nav-link[href]").map(function (a) {
            return { label: a.textContent.trim(), icon: ($("i", a) || {}).className || "fa-solid fa-arrow-right", href: a.href, group: "Go to" };
        });
        entries.push({ label: "Toggle dark mode", icon: "fa-solid fa-circle-half-stroke", group: "Actions", run: function () { if (themeBtn) themeBtn.click(); } });
        entries.push({ label: "Sign out", icon: "fa-solid fa-arrow-right-from-bracket", group: "Actions", run: function () { var f = $(".side-logout"); if (f) f.submit(); } });

        var render = function () {
            var q = input.value.trim().toLowerCase();
            items = entries.filter(function (e) { return !q || e.label.toLowerCase().indexOf(q) !== -1; });
            active = Math.min(active, Math.max(0, items.length - 1));
            list.innerHTML = items.length ? items.map(function (e, i) {
                return '<li role="option" data-i="' + i + '" class="' + (i === active ? "active" : "") + '"><i class="' + escapeHtml(e.icon) + '"></i><span>' +
                    escapeHtml(e.label) + '</span><small>' + e.group + "</small></li>";
            }).join("") : '<li class="palette-empty">No page or action matches that.</li>';
        };
        var open = function () { palette.hidden = false; input.value = ""; active = 0; render(); setTimeout(function () { input.focus(); }, 10); };
        var close = function () { palette.hidden = true; };
        var go = function (e) { if (!e) return; close(); if (e.run) e.run(); else window.location = e.href; };

        $all("[data-palette-open]").forEach(function (b) { b.addEventListener("click", open); });
        $all("[data-palette-close]").forEach(function (b) { b.addEventListener("click", close); });
        input.addEventListener("input", function () { active = 0; render(); });
        list.addEventListener("click", function (e) { var li = e.target.closest("li[data-i]"); if (li) go(items[+li.getAttribute("data-i")]); });
        document.addEventListener("keydown", function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") { e.preventDefault(); palette.hidden ? open() : close(); return; }
            if (palette.hidden) return;
            if (e.key === "Escape") close();
            else if (e.key === "ArrowDown") { e.preventDefault(); active = Math.min(items.length - 1, active + 1); render(); }
            else if (e.key === "ArrowUp") { e.preventDefault(); active = Math.max(0, active - 1); render(); }
            else if (e.key === "Enter") { e.preventDefault(); go(items[active]); }
        });
    }

    /* ---------- Password fields: show/hide and strength ---------- */
    $all("input[type=password]").forEach(function (pw) {
        var wrap = document.createElement("div");
        wrap.className = "pw-wrap";
        pw.parentNode.insertBefore(wrap, pw);
        wrap.appendChild(pw);
        var btn = document.createElement("button");
        btn.type = "button"; btn.className = "pw-toggle"; btn.setAttribute("aria-label", "Show password");
        btn.innerHTML = '<i class="fa-regular fa-eye-slash"></i>';
        btn.addEventListener("click", function () {
            var show = pw.type === "password";
            pw.type = show ? "text" : "password";
            btn.setAttribute("aria-label", show ? "Hide password" : "Show password");
            btn.innerHTML = '<i class="fa-regular ' + (show ? "fa-eye" : "fa-eye-slash") + '"></i>';
        });
        wrap.appendChild(btn);

        if (pw.hasAttribute("data-strength")) {
            var meter = document.createElement("div");
            meter.className = "pw-meter";
            meter.innerHTML = "<span></span><span></span><span></span><span></span><em></em>";
            wrap.parentNode.insertBefore(meter, wrap.nextSibling);
            var labels = ["", "Weak", "Fair", "Good", "Strong"];
            pw.addEventListener("input", function () {
                var v = pw.value, score = 0;
                if (v.length >= 8) score++;
                if (/[A-Z]/.test(v) && /[a-z]/.test(v)) score++;
                if (/\d/.test(v)) score++;
                if (/[^A-Za-z0-9]/.test(v) || v.length >= 14) score++;
                if (!v) score = 0;
                meter.setAttribute("data-score", score);
                $("em", meter).textContent = labels[score];
            });
        }
    });

    /* ---------- File upload boxes: check type and size before anything is sent ---------- */
    $all("[data-dropzone]").forEach(function (zone) {
        var input = $("input[type=file]", zone), nameEl = $("[data-dz-name]", zone), metaEl = $("[data-dz-meta]", zone), clear = $("[data-dz-clear]", zone);
        var maxMb = parseFloat(zone.getAttribute("data-max-mb")) || 10;
        var allowed = (zone.getAttribute("data-accept") || "").split(",");
        var original = { name: nameEl.textContent, meta: metaEl.textContent };
        var size = function (b) { return b > 1048576 ? (b / 1048576).toFixed(1) + " MB" : Math.max(1, Math.round(b / 1024)) + " KB"; };

        var update = function () {
            var f = input.files && input.files[0];
            zone.classList.remove("has-file", "is-invalid");
            input.setCustomValidity("");
            if (!f) { nameEl.textContent = original.name; metaEl.textContent = original.meta; clear.hidden = true; return; }
            var ext = "." + (f.name.split(".").pop() || "").toLowerCase();
            var problem = allowed.length && allowed[0] && allowed.indexOf(ext) === -1 ? "That file type isn't accepted. Use " + allowed.map(function (a) { return a.replace(".", "").toUpperCase(); }).filter(function (a, i, arr) { return arr.indexOf(a) === i && a !== "JPEG"; }).join(", ").replace(/, ([^,]*)$/, " or $1") + "."
                : f.size > maxMb * 1048576 ? "This file is " + size(f.size) + ". The limit is " + maxMb + " MB; try a PDF scan or a smaller photo." : "";
            nameEl.textContent = f.name;
            metaEl.textContent = problem || size(f.size) + ", ready to upload";
            zone.classList.add(problem ? "is-invalid" : "has-file");
            input.setCustomValidity(problem);
            clear.hidden = false;
        };
        input.addEventListener("change", update);
        clear.addEventListener("click", function (e) { e.preventDefault(); input.value = ""; update(); });
        ["dragenter", "dragover"].forEach(function (t) { zone.addEventListener(t, function (e) { e.preventDefault(); zone.classList.add("is-drag"); }); });
        ["dragleave", "drop"].forEach(function (t) { zone.addEventListener(t, function () { zone.classList.remove("is-drag"); }); });
        zone.addEventListener("drop", function (e) {
            e.preventDefault();
            if (e.dataTransfer && e.dataTransfer.files.length) { input.files = e.dataTransfer.files; update(); }
        });
        var form = zone.closest("form");
        if (form && !form.hasAttribute("data-dz-bound")) {
            form.setAttribute("data-dz-bound", "1");
            form.addEventListener("submit", function (e) {
                // required documents: nothing chosen yet
                $all("[data-dropzone][data-required]", form).forEach(function (z) {
                    var inp = $("input[type=file]", z);
                    if (!inp.files || !inp.files.length) {
                        z.classList.remove("has-file");
                        z.classList.add("is-invalid");
                        $("[data-dz-meta]", z).textContent = "This document is required. Choose a file to continue.";
                    }
                });
                var bad = $all("[data-dropzone].is-invalid", form);
                if (bad.length) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    toast("Check your files", $("[data-dz-meta]", bad[0]).textContent, "error");
                    bad[0].scrollIntoView({ behavior: "smooth", block: "center" });
                }
            }, true);
        }
    });

    /* ---------- Top-bar clock in South African time (SAST, UTC+2) ---------- */
    var clockEl = $("[data-sast-clock]");
    if (clockEl) {
        var days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
        var months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
        var tick = function () {
            var d = new Date(Date.now() + 2 * 3600000); // UTC+2, read with the UTC getters
            clockEl.textContent = days[d.getUTCDay()] + " " + d.getUTCDate() + " " + months[d.getUTCMonth()] + ", " +
                ("0" + d.getUTCHours()).slice(-2) + ":" + ("0" + d.getUTCMinutes()).slice(-2) + " SAST";
        };
        tick();
        setInterval(tick, 30000);
    }

    /* ---------- Find a tutor: live search through the REST API (GET /api/v1/tutors?q=) ---------- */
    var live = $("[data-live-search]");
    if (live) {
        var qInput = $("input[type=search]", live), results = $("[data-tutor-results]"), status = $("[data-live-status]");
        var palette = ["#4F46E5", "#6C63FF", "#0E7490", "#15803D", "#B45309", "#BE123C", "#7C3AED", "#0369A1", "#4D7C0F"];
        var colour = function (name) {           // same hash as Ui.AvatarColor on the server
            var h = 17;
            for (var i = 0; i < name.length; i++) h = (Math.imul(h, 31) + name.charCodeAt(i)) | 0;
            return palette[(h & 0x7fffffff) % palette.length];
        };
        var initials = function (name) {
            var p = name.trim().split(/\s+/);
            return (p[0].charAt(0) + (p.length > 1 ? p[p.length - 1].charAt(0) : "")).toUpperCase();
        };
        var render = function (tutors, q) {
            if (!tutors.length) {
                results.innerHTML = '<div class="card"><div class="empty"><i class="fa-solid fa-user-slash"></i><p>' +
                    (q ? "No tutors match \u201C" + escapeHtml(q) + "\u201D for your year of study." : "No tutors have been assigned to your modules yet.") + "</p></div></div>";
                return;
            }
            results.innerHTML = '<div class="grid grid-3">' + tutors.map(function (t) {
                var open = t.openSlots;
                return '<div class="card tutor-card"><div class="card-body"><div class="person">' +
                    '<span class="avatar lg" style="background:' + colour(t.name) + '" aria-hidden="true">' + escapeHtml(initials(t.name)) + '</span>' +
                    '<div><div class="person-name">' + escapeHtml(t.name) + '</div><div class="muted small">' + escapeHtml(t.email) + '</div></div></div>' +
                    '<div class="module-chips">' + t.modules.map(function (m) { return '<span class="tag" title="' + escapeHtml(m.name) + '">' + escapeHtml(m.code) + "</span>"; }).join("") + "</div>" +
                    '<p class="small text-2" style="margin:12px 0 0">' + t.modules.map(function (m) { return escapeHtml(m.name); }).join(", ") + "</p></div>" +
                    '<div class="card-foot spread"><span class="small ' + (open > 0 ? "text-2" : "muted") + '"><i class="fa-regular fa-clock"></i> ' +
                    (open === 0 ? "No open slots" : open + (open === 1 ? " open slot" : " open slots")) + "</span>" +
                    '<a class="btn btn-primary btn-sm' + (open === 0 ? " disabled" : "") + '" href="' + live.getAttribute("data-book") + "?tutorId=" + t.id + '">Book</a></div></div>';
            }).join("") + "</div>";
        };
        var timer, seq = 0;
        qInput.addEventListener("input", function () {
            clearTimeout(timer);
            timer = setTimeout(function () {
                var q = qInput.value.trim(), mine = ++seq;
                status.textContent = "Searching\u2026";
                getJson(live.getAttribute("data-api") + (q ? "?q=" + encodeURIComponent(q) : "")).then(function (body) {
                    if (mine !== seq) return;          // a newer search has started
                    render(body.data || [], q);
                    status.textContent = (body.data || []).length + ((body.data || []).length === 1 ? " tutor" : " tutors") + " found";
                    if (window.history && history.replaceState) history.replaceState(null, "", "?q=" + encodeURIComponent(q));
                }).catch(function () { status.textContent = "Live search is unavailable; press Search instead."; });
            }, 250);
        });
    }

    /* ---------- Faculty -> department -> course: each list unlocks only after the one before it ---------- */
    $all("[data-cascade]").forEach(function (box) {
        var dataEl = $("[data-cascade-data]", box);
        if (!dataEl) return;
        var data = JSON.parse(dataEl.textContent || "{}");
        var fac = $("[data-cascade-faculty]", box), dep = $("[data-cascade-department]", box), crs = $("[data-cascade-course]", box);
        var stepOf = function (el) { return el ? el.closest(".cascade-step") : null; };
        var setStep = function (el, state) {
            var st = stepOf(el);
            if (!st) return;
            st.classList.remove("is-done", "is-current", "is-locked");
            st.classList.add(state);
        };
        var fill = function (select, items, placeholder, current) {
            select.innerHTML = "";
            var first = document.createElement("option");
            first.value = ""; first.textContent = placeholder;
            select.appendChild(first);
            items.forEach(function (it) {
                var o = document.createElement("option");
                o.value = it.id; o.textContent = it.name;
                if (String(it.id) === String(current)) o.selected = true;
                select.appendChild(o);
            });
        };
        var updateCourses = function (keep) {
            if (!crs) return;
            var d = dep.value;
            if (!d) {
                fill(crs, [], "Choose a department first");
                crs.disabled = true; setStep(crs, "is-locked"); return;
            }
            var list = (data.courses || []).filter(function (c) { return String(c.department) === d; });
            fill(crs, list, list.length ? "Choose your course" : "No courses in this department yet", keep ? crs.getAttribute("data-current") : null);
            crs.disabled = list.length === 0;
            setStep(crs, crs.value ? "is-done" : "is-current");
        };
        var updateDepartments = function (keep) {
            var f = fac.value;
            setStep(fac, f ? "is-done" : "is-current");
            if (!f) {
                fill(dep, [], "Choose a faculty first");
                dep.disabled = true; setStep(dep, "is-locked");
            } else {
                var list = (data.departments || []).filter(function (d) { return String(d.faculty) === f; });
                fill(dep, list, list.length ? "Choose a department" : "No departments in this faculty yet", keep ? dep.getAttribute("data-current") : null);
                dep.disabled = list.length === 0;
                setStep(dep, dep.value ? "is-done" : "is-current");
            }
            updateCourses(keep);
        };
        dep.setAttribute("data-current", dep.value);
        if (crs) crs.setAttribute("data-current", crs.value);
        fac.addEventListener("change", function () { updateDepartments(false); if (!dep.disabled) dep.focus(); });
        dep.addEventListener("change", function () { setStep(dep, dep.value ? "is-done" : "is-current"); updateCourses(false); if (crs && !crs.disabled) crs.focus(); });
        if (crs) crs.addEventListener("change", function () { setStep(crs, crs.value ? "is-done" : "is-current"); });
        updateDepartments(true);
    });

    /* ---------- AI study assistant ---------- */
    // Minimal, safe Markdown: everything is HTML-escaped first, then a few patterns are formatted.
    function renderMarkdown(text) {
        var blocks = [], out = [];
        var src = escapeHtml(text).replace(/```[a-zA-Z]*\n?([\s\S]*?)```/g, function (m, code) {
            blocks.push("<pre><code>" + code.replace(/\n$/, "") + "</code></pre>");
            return "\u0000" + (blocks.length - 1) + "\u0000";
        });
        var inline = function (t) {
            return t.replace(/`([^`]+)`/g, "<code>$1</code>")
                    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
                    .replace(/(^|[^*])\*([^*\n]+)\*/g, "$1<em>$2</em>");
        };
        var list = null;
        var closeList = function () { if (list) { out.push("</" + list + ">"); list = null; } };
        src.split("\n").forEach(function (line) {
            var m;
            if (/^\u0000\d+\u0000$/.test(line.trim())) { closeList(); out.push(line.trim()); }
            else if ((m = line.match(/^(#{1,4})\s+(.*)$/))) { closeList(); var lvl = Math.min(4, m[1].length + 2); out.push("<h" + lvl + ">" + inline(m[2]) + "</h" + lvl + ">"); }
            else if ((m = line.match(/^\s*[-*]\s+(.*)$/))) { if (list !== "ul") { closeList(); out.push("<ul>"); list = "ul"; } out.push("<li>" + inline(m[1]) + "</li>"); }
            else if ((m = line.match(/^\s*\d+[.)]\s+(.*)$/))) { if (list !== "ol") { closeList(); out.push("<ol>"); list = "ol"; } out.push("<li>" + inline(m[1]) + "</li>"); }
            else if (!line.trim()) { closeList(); }
            else { closeList(); out.push("<p>" + inline(line) + "</p>"); }
        });
        closeList();
        return out.join("").replace(/\u0000(\d+)\u0000/g, function (m, i) { return blocks[+i]; });
    }
    $all("[data-ai-md]").forEach(function (el) {
        if (el.closest(".ai-assistant")) el.innerHTML = renderMarkdown(el.textContent);
    });

    var chat = $("[data-ai-chat]");
    if (chat) {
        var msgs = $("[data-ai-messages]", chat), form = $("[data-ai-form]", chat), box = $("textarea", form);
        var sendBtn = $("button[type=submit]", form), remainingEl = $("[data-ai-remaining]");
        var scroll = function () { msgs.scrollTop = msgs.scrollHeight; };
        var add = function (role, html) {
            var w = $("[data-ai-welcome]", msgs); if (w) w.remove();
            var d = document.createElement("div");
            d.className = "ai-msg ai-" + role;
            d.innerHTML = '<div class="ai-bubble">' + html + "</div>";
            msgs.appendChild(d); scroll();
            return d;
        };
        var busy = false;
        var send = function (text, mode) {
            if (busy || !text.trim() || box.disabled) return;
            busy = true; sendBtn.disabled = true;
            add("user", escapeHtml(text).replace(/\n/g, "<br>"));
            var thinking = add("assistant", '<span class="ai-typing"><i></i><i></i><i></i></span>');
            box.value = "";
            post(chat.getAttribute("data-url"), { module: chat.getAttribute("data-module"), mode: mode, message: text })
                .then(function (d) {
                    $(".ai-bubble", thinking).innerHTML = renderMarkdown(d.answer);
                    if (remainingEl && typeof d.remaining === "number") remainingEl.textContent = d.remaining;
                    var engineLabel = $("[data-ai-engine-label]");
                    if (engineLabel && d.provider) engineLabel.textContent = d.provider;
                })
                .catch(function (err) {
                    thinking.classList.add("ai-error");
                    $(".ai-bubble", thinking).innerHTML = '<i class="fa-solid fa-circle-exclamation"></i> ' + escapeHtml(err.message);
                })
                .then(function () { busy = false; sendBtn.disabled = false; box.focus(); scroll(); });
        };
        var currentMode = function () { var r = $("input[name=mode]:checked", form); return r ? r.value : "explain"; };
        form.addEventListener("submit", function (e) { e.preventDefault(); e.stopImmediatePropagation(); send(box.value, currentMode()); }, true);
        box.addEventListener("keydown", function (e) {
            if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(box.value, currentMode()); }
        });
        $all("[data-ai-suggest]", chat).forEach(function (b) {
            b.addEventListener("click", function () {
                var r = $("input[name=mode][value='" + b.getAttribute("data-mode") + "']", form);
                if (r) r.checked = true;
                send(b.textContent, b.getAttribute("data-mode"));
            });
        });
        scroll();
    }

    /* ---------- Year -> module pickers ----------
       <select data-year-source="#moduleSelect"> filters #moduleSelect's options by their data-year,
       and keeps the module list locked until a year is chosen. */
    $all("[data-year-source]").forEach(function (yearSel) {
        var mod = $(yearSel.getAttribute("data-year-source"));
        if (!mod) return;
        var placeholder = mod.options[0];
        var sync = function () {
            var y = yearSel.value, count = 0;
            $all("option[data-year]", mod).forEach(function (o) {
                var ok = !!y && o.getAttribute("data-year") === y;
                o.hidden = !ok; o.disabled = !ok;
                if (ok) count++;
            });
            var cur = mod.options[mod.selectedIndex];
            if (cur && cur.hidden) mod.value = "";
            mod.disabled = !y || count === 0;
            if (placeholder && !placeholder.value) placeholder.textContent = !y ? "Choose a year first" : count === 0 ? "No modules in that year" : "Choose a module";
            mod.dispatchEvent(new Event("change"));
        };
        yearSel.addEventListener("change", sync);
        sync();
    });

    /* ---------- Lecturer: assign a tutor ---------- */
    var assign = $("[data-assign-form]");
    if (assign) {
        var aYear = $("[data-assign-year]", assign), aMod = $("[data-assign-module]", assign), aHint = $("[data-assign-module-hint]", assign);
        var aSearch = $("[data-assign-search]", assign), aOnly = $("[data-assign-only-module]", assign), aEmpty = $("[data-assign-empty]", assign);
        var aPlaceholder = aMod.options[0];
        var filterTutors = function () {
            var opt = aMod.options[aMod.selectedIndex], code = opt && opt.value ? opt.getAttribute("data-code") : "";
            var q = aSearch.value.trim().toLowerCase(), shown = 0;
            $all(".tutor-option", assign).forEach(function (row) {
                var mods = (row.getAttribute("data-modules") || "").split(",");
                var ok = (!q || row.textContent.toLowerCase().indexOf(q) !== -1) && (!aOnly.checked || !code || mods.indexOf(code) !== -1);
                row.hidden = !ok;
                if (!ok) { var r = $("input", row); if (r.checked) r.checked = false; }
                if (ok) shown++;
            });
            aEmpty.hidden = shown !== 0;
        };
        var syncModules = function () {
            var y = aYear.value, count = 0;
            $all("option[data-year]", aMod).forEach(function (o) {
                var ok = !!y && o.getAttribute("data-year") === y;
                o.hidden = !ok; o.disabled = !ok; if (ok) count++;
            });
            if (aMod.options[aMod.selectedIndex] && aMod.options[aMod.selectedIndex].hidden) aMod.value = "";
            aMod.disabled = !y || count === 0;
            aPlaceholder.textContent = !y ? "Choose a year first" : count === 0 ? "You have no modules in that year" : "Choose a module";
            syncHint();
        };
        var syncHint = function () {
            var opt = aMod.options[aMod.selectedIndex];
            if (!opt || !opt.value) { aHint.textContent = ""; filterTutors(); return; }
            var open = parseInt(opt.getAttribute("data-open"), 10) || 0;
            aHint.textContent = open > 0 ? open + (open === 1 ? " position" : " positions") + " still open." : "All positions are filled. Increase \u201Ctutors needed\u201D above to add another tutor.";
            filterTutors();
        };
        aYear.addEventListener("change", syncModules);
        aMod.addEventListener("change", syncHint);
        aSearch.addEventListener("input", filterTutors);
        aOnly.addEventListener("change", filterTutors);
        syncModules();
    }

    /* ---------- Search inside a long dropdown: <input data-option-filter="#select"> ---------- */
    $all("[data-option-filter]").forEach(function (input) {
        var sel = $(input.getAttribute("data-option-filter"));
        if (!sel) return;
        input.addEventListener("input", function () {
            var q = input.value.trim().toLowerCase(), first = null;
            $all("option", sel).forEach(function (o) {
                if (!o.value) return;
                var ok = !o.disabled && (!q || o.textContent.toLowerCase().indexOf(q) !== -1);
                o.hidden = !ok;
                if (ok && !first) first = o;
            });
            $all("optgroup", sel).forEach(function (g) { g.hidden = !$all("option", g).some(function (o) { return !o.hidden; }); });
            if (q && first) sel.value = first.value;
        });
    });

    /* ---------- Year tabs: <div data-year-tabs> with buttons [data-year-tab] and panels [data-year-panel] ---------- */
    $all("[data-year-tabs]").forEach(function (box) {
        var tabs = $all("[data-year-tab]", box), panels = $all("[data-year-panel]", box);
        var linkYear = $("[data-link-year]", box);
        var show = function (y, fromSelect) {
            tabs.forEach(function (t) {
                var on = t.getAttribute("data-year-tab") === y;
                t.classList.toggle("active", on);
                t.setAttribute("aria-selected", on ? "true" : "false");
            });
            panels.forEach(function (p) { p.hidden = p.getAttribute("data-year-panel") !== y; });
            if (linkYear && !fromSelect && linkYear.value !== y) {
                linkYear.value = y;
                linkYear.dispatchEvent(new Event("change"));
            }
        };
        tabs.forEach(function (t) { t.addEventListener("click", function () { show(t.getAttribute("data-year-tab")); }); });
        if (linkYear) linkYear.addEventListener("change", function () { if (linkYear.value) show(linkYear.value, true); });
        var first = box.getAttribute("data-default-year") || (tabs[0] && tabs[0].getAttribute("data-year-tab"));
        if (first) show(first);
    });

    /* ---------- Repeatable rows (modules): filling in the last row opens a new one; no limit ---------- */
    $all("[data-repeat]").forEach(function (box) {
        var list = $("[data-repeat-list]", box), countEl = $("[data-repeat-count]", box);
        var rows = function () { return $all("[data-repeat-row]", list); };
        var isFilled = function (row) { return $all("[data-row-key]", row).some(function (i) { return i.value.trim() !== ""; }); };
        var template = rows()[0].cloneNode(true);

        var renumber = function () {
            var all = rows();
            all.forEach(function (row, i) {
                $all("[name]", row).forEach(function (el) { el.name = el.name.replace(/\[\d+\]/, "[" + i + "]"); });
                var rm = $("[data-repeat-remove]", row);
                if (rm) rm.disabled = all.length === 1;
            });
            var filled = all.filter(isFilled).length;
            if (countEl) countEl.textContent = filled === 0 ? "" : filled + (filled === 1 ? " module" : " modules") + " ready to add";
        };
        var addRow = function (focus) {
            var all = rows(), last = all[all.length - 1];
            var row = template.cloneNode(true);
            $all("input", row).forEach(function (i) { i.value = ""; });
            // carry the year and semester over: modules are usually added a year at a time
            var lastSelects = $all("select", last), newSelects = $all("select", row);
            newSelects.forEach(function (sel, i) { if (lastSelects[i] && !/LecturerId/.test(sel.name)) sel.value = lastSelects[i].value; });
            list.appendChild(row);
            renumber();
            if (focus) $("input", row).focus();
            return row;
        };
        list.addEventListener("input", function (e) {
            var row = e.target.closest("[data-repeat-row]"), all = rows();
            if (row && row === all[all.length - 1] && e.target.hasAttribute("data-row-key") && e.target.value.trim() !== "") addRow(false);
            renumber();
        });
        list.addEventListener("click", function (e) {
            var btn = e.target.closest("[data-repeat-remove]");
            if (!btn) return;
            var row = btn.closest("[data-repeat-row]");
            if (rows().length > 1) row.remove();
            var all = rows();
            if (isFilled(all[all.length - 1])) addRow(false);   // always leave one empty row to type into
            renumber();
        });
        var add = $("[data-repeat-add]", box);
        if (add) add.addEventListener("click", function () {
            var all = rows(), last = all[all.length - 1];
            if (!isFilled(last)) { $("input", last).focus(); return; }
            addRow(true);
        });
        // start with an empty row at the end
        if (isFilled(rows()[rows().length - 1])) addRow(false);
        renumber();
    });


    /* ---------- Bank account numbers: exactly the length(s) the chosen bank uses ---------- */
    function accountGuard(input, hint) {
        var lengths = null, bankName = "", lengthText = "";
        var digitsOf = function () { return (input.value || "").replace(/\D/g, ""); };
        var render = function () {
            var max = lengths ? Math.max.apply(null, lengths) : 16;
            var d = digitsOf().slice(0, max);          // never more digits than the bank uses
            var grouped = d.replace(/(\d{4})(?=\d)/g, "$1 ");
            if (input.value !== grouped) input.value = grouped;
            input.maxLength = max + Math.floor((max - 1) / 4);   // digits plus the grouping spaces
            if (!hint) return;
            hint.classList.remove("ok", "bad");
            if (!lengths) { hint.textContent = "Choose your bank first."; return; }
            var ok = lengths.indexOf(d.length) !== -1;
            var progress = lengths.length === 1 ? d.length + " of " + lengths[0] + " entered" : d.length + (d.length === 1 ? " digit" : " digits") + " entered";
            hint.textContent = bankName + " account numbers are " + lengthText + (d.length ? ": " + progress : ".");
            if (d.length) hint.classList.add(ok ? "ok" : "bad");
        };
        input.addEventListener("input", function () { input.classList.remove("input-validation-error"); render(); });
        input.addEventListener("paste", function () { setTimeout(render, 0); });
        return {
            setBank: function (lens, text, name) { lengths = lens; lengthText = text; bankName = name; render(); },
            isValid: function () { return !!lengths && lengths.indexOf(digitsOf().length) !== -1; },
            problem: function () {
                if (!lengths) return "Choose your bank first.";
                var n = digitsOf().length;
                return n === 0 ? "Enter the account number." : bankName + " account numbers are " + lengthText + ". You entered " + n + ".";
            },
            render: render
        };
    }
    var parseLengths = function (el) { return (el.getAttribute("data-lengths") || "").split(",").filter(Boolean).map(Number); };

    // Tutor banking form: <select data-bank-select data-account-input="#..." data-branch-input="#...">
    $all("[data-bank-select]").forEach(function (sel) {
        var input = $(sel.getAttribute("data-account-input")), branchIn = $(sel.getAttribute("data-branch-input"));
        if (!input) return;
        var guard = accountGuard(input, $("[data-account-hint]", input.parentNode));
        var sync = function () {
            var o = sel.options[sel.selectedIndex];
            if (!o || !o.value) { guard.setBank(null, "", ""); return; }
            guard.setBank(parseLengths(o), o.getAttribute("data-length-text"), o.value);
            if (branchIn && o.getAttribute("data-branch")) branchIn.value = o.getAttribute("data-branch");
            if (input.placeholder === "Choose your bank first") input.placeholder = o.getAttribute("data-length-text");
        };
        sel.addEventListener("change", sync);
        sync();
        var form = sel.closest("form");
        if (form) form.addEventListener("submit", function (e) {
            if (input.value.trim() && !guard.isValid()) {
                e.preventDefault(); e.stopImmediatePropagation();
                toast("Check the account number", guard.problem(), "error");
                input.classList.add("input-validation-error"); input.focus();
            }
        }, true);
    });

    /* ---------- Bank payment checkout ---------- */
    var payForm = $("[data-pay-form]");
    if (payForm) {
        var steps = $all("[data-step]", payForm), dots = $all("[data-step-dot]", payForm);
        var holder = $("#accountHolder", payForm), number = $("[data-account-number]", payForm), branch = $("#branchCode", payForm);
        var accType = $("#accountType", payForm), authorise = $("[data-authorise]", payForm), payBtn = $("[data-pay]", payForm);
        var next1 = $("[data-next='2']", payForm);
        var chosen = function () { return $("input[name=bankCode]:checked", payForm); };
        var digits = function () { return (number.value || "").replace(/[\s-]/g, ""); };

        var show = function (n) {
            steps.forEach(function (st) { st.hidden = st.getAttribute("data-step") !== String(n); });
            dots.forEach(function (d) {
                var k = +d.getAttribute("data-step-dot");
                d.classList.toggle("active", k === n);
                d.classList.toggle("done", k < n);
            });
            var first = $("[data-step='" + n + "'] input:not([type=radio]):not([readonly]), [data-step='" + n + "'] select", payForm);
            if (first && n === 2) first.focus();
            window.scrollTo({ top: 0, behavior: "smooth" });
        };
        var fail = function (msg, el) {
            toast("Check your details", msg, "error");
            if (el) { el.classList.add("input-validation-error"); el.focus(); }
        };
        var syncBank = function () {
            var b = chosen();
            next1.disabled = !b;
            if (!b) return;
            branch.value = b.getAttribute("data-branch");
            guard.setBank(parseLengths(b), b.getAttribute("data-length-text"), b.getAttribute("data-bank-name"));
            $all("[data-chosen-bank]", payForm).forEach(function (e) { e.textContent = b.getAttribute("data-bank-name"); });
        };
        $all("input[name=bankCode]", payForm).forEach(function (r) {
            r.addEventListener("change", syncBank);
            r.addEventListener("dblclick", function () { syncBank(); show(2); });
        });
        var guard = accountGuard(number, $("[data-account-hint]", payForm));
        holder.addEventListener("input", function () { holder.classList.remove("input-validation-error"); });

        $all("[data-next]", payForm).forEach(function (btn) {
            btn.addEventListener("click", function () {
                var to = +btn.getAttribute("data-next");
                if (to === 2 && !chosen()) return fail("Choose your bank.");
                if (to === 3) {
                    if (holder.value.trim().length < 3) return fail("Enter the account holder's name as it appears at your bank.", holder);
                    if (!guard.isValid()) return fail(guard.problem(), number);
                    var d = digits();
                    $("[data-review-from-bank]", payForm).textContent = chosen().getAttribute("data-bank-name");
                    $("[data-review-from-holder]", payForm).textContent = holder.value.trim() + ", " + accType.value.toLowerCase();
                    $("[data-review-from-number]", payForm).textContent = "•••• •••• " + d.slice(-4);
                }
                show(to);
            });
        });
        $all("[data-back]", payForm).forEach(function (btn) { btn.addEventListener("click", function () { show(+btn.getAttribute("data-back")); }); });
        authorise.addEventListener("change", function () { payBtn.disabled = !authorise.checked; });

        payForm.addEventListener("submit", function (e) {
            if (payForm.getAttribute("data-confirmed")) return;
            e.preventDefault();
            e.stopImmediatePropagation();
            if (!authorise.checked) return fail("Tick the box to authorise the payment.");
            var overlay = $("[data-processing]", payForm), text = $("[data-processing-text]", payForm);
            overlay.hidden = false;
            var msgs = ["Contacting your bank…", "Authorising payment…", "Confirming with your tutor's bank…"];
            msgs.forEach(function (m, i) { setTimeout(function () { text.textContent = m; }, i * 900); });
            setTimeout(function () { payForm.setAttribute("data-confirmed", "1"); payForm.submit(); }, msgs.length * 900);
        }, true);

        syncBank();
        // returned with a server-side error: go straight back to the account step
        if ($(".alert-error", payForm) && chosen()) show(2); else show(1);
    }

    /* ---------- Card checkout: the full number and CVV never leave the browser ---------- */
    var cardForm = $("[data-card-form]");
    if (cardForm) {
        var num = $("[data-card-number]", cardForm), exp = $("[data-card-expiry]", cardForm), cvv = $("[data-card-cvv]", cardForm);
        var holderIn = $("#cardholderName", cardForm), okIcon = $("[data-card-ok]", cardForm), hintEl = $("[data-card-hint]", cardForm);
        var brandChips = $all("[data-brand]", cardForm);
        var onlyDigits = function (v) { return (v || "").replace(/\D/g, ""); };
        var brandOf = function (d) {
            if (/^4/.test(d)) return "Visa";
            if (/^3[47]/.test(d)) return "American Express";
            var two = +d.slice(0, 2), four = +d.slice(0, 4);
            if ((two >= 51 && two <= 55) || (four >= 2221 && four <= 2720)) return "Mastercard";
            return null;
        };
        var lengthsFor = function (b) { return b === "American Express" ? [15] : b === "Visa" ? [13, 16, 19] : [16]; };
        var luhn = function (d) {
            var sum = 0, dbl = false;
            for (var i = d.length - 1; i >= 0; i--) {
                var n = +d.charAt(i);
                if (dbl) { n *= 2; if (n > 9) n -= 9; }
                sum += n; dbl = !dbl;
            }
            return d.length > 0 && sum % 10 === 0;
        };
        var cardState = function () {
            var d = onlyDigits(num.value), b = brandOf(d);
            return { digits: d, brand: b, valid: !!b && lengthsFor(b).indexOf(d.length) !== -1 && luhn(d) };
        };
        var renderNumber = function () {
            var d = onlyDigits(num.value), b = brandOf(d), max = b ? Math.max.apply(null, lengthsFor(b)) : 19;
            d = d.slice(0, max);
            num.value = b === "American Express" ? d.replace(/^(\d{4})(\d{0,6})(\d{0,5}).*/, function (m, a, c, e) { return [a, c, e].filter(Boolean).join(" "); })
                                                 : d.replace(/(\d{4})(?=\d)/g, "$1 ");
            var st = cardState();
            brandChips.forEach(function (c) { c.classList.toggle("on", c.getAttribute("data-brand") === st.brand); c.classList.toggle("dim", !!st.brand && c.getAttribute("data-brand") !== st.brand); });
            okIcon.hidden = !st.valid;
            num.parentNode.classList.toggle("is-valid", st.valid);
            num.classList.remove("input-validation-error");
            cvv.placeholder = st.brand === "American Express" ? "4 digits" : "3 digits";
            if (!d.length) hintEl.textContent = "";
            else if (!st.brand) hintEl.textContent = "We accept Visa, Mastercard and American Express.";
            else if (lengthsFor(st.brand).indexOf(d.length) === -1) hintEl.textContent = st.brand + " card numbers are " + lengthsFor(st.brand).join(" or ") + " digits.";
            else if (!st.valid) hintEl.textContent = "That card number isn't valid. Check for a typing mistake.";
            else hintEl.textContent = "";
        };
        num.addEventListener("input", renderNumber);
        exp.addEventListener("input", function (e) {
            var d = onlyDigits(exp.value).slice(0, 4);
            if (d.length === 1 && +d > 1) d = "0" + d;                   // "4" -> "04"
            exp.value = d.length > 2 ? d.slice(0, 2) + " / " + d.slice(2) : (d.length === 2 && e.inputType !== "deleteContentBackward" ? d + " / " : d);
            exp.classList.remove("input-validation-error");
        });
        cvv.addEventListener("input", function () { cvv.value = onlyDigits(cvv.value).slice(0, 4); cvv.classList.remove("input-validation-error"); });
        $("[data-cvv-help]", cardForm).addEventListener("click", function () {
            var t = $("[data-cvv-text]", cardForm); t.hidden = !t.hidden;
            this.setAttribute("aria-expanded", t.hidden ? "false" : "true");
        });

        // optional: remember the billing address on this device only
        var billing = $all("[data-billing]", cardForm), remember = $("[data-remember-billing]", cardForm), store = "tc-billing";
        try {
            var saved = JSON.parse(localStorage.getItem(store) || "null");
            if (saved) { billing.forEach(function (f) { if (!f.value || f.tagName === "SELECT") f.value = saved[f.name] || f.value; }); remember.checked = true; }
        } catch (e) { }

        var expiryProblem = function () {
            var m = /^(\d{2}) \/ (\d{2})$/.exec(exp.value);
            if (!m) return "Enter the expiry date as MM / YY.";
            var mm = +m[1], yy = 2000 + +m[2], now = new Date();
            if (mm < 1 || mm > 12) return "The expiry month must be between 01 and 12.";
            if (yy < now.getFullYear() || (yy === now.getFullYear() && mm < now.getMonth() + 1)) return "This card has expired.";
            return null;
        };
        var stop = function (msg, el) { toast("Check your card details", msg, "error"); if (el) { el.classList.add("input-validation-error"); el.focus(); } };

        cardForm.addEventListener("submit", function (e) {
            if (cardForm.getAttribute("data-confirmed")) return;
            e.preventDefault(); e.stopImmediatePropagation();
            var st = cardState();
            if (holderIn.value.trim().length < 3) return stop("Enter the cardholder name as it appears on the card.", holderIn);
            if (!st.valid) return stop(st.brand ? "That card number isn't valid. Check for a typing mistake." : "Enter a Visa, Mastercard or American Express card number.", num);
            var ep = expiryProblem(); if (ep) return stop(ep, exp);
            var needCvv = st.brand === "American Express" ? 4 : 3;
            if (onlyDigits(cvv.value).length !== needCvv) return stop("Enter the " + needCvv + "-digit security code (CVV).", cvv);
            var street = $("#street", cardForm), city = $("#city", cardForm), postal = $("#postalCode", cardForm), country = $("#country", cardForm);
            if (!street.value.trim()) return stop("Enter your billing street address.", street);
            if (!city.value.trim()) return stop("Enter your city.", city);
            if (country.value === "South Africa" ? !/^\d{4}$/.test(postal.value.trim()) : !postal.value.trim()) return stop(country.value === "South Africa" ? "South African postal codes have 4 digits." : "Enter your postal code.", postal);

            try {
                if (remember.checked) { var o = {}; billing.forEach(function (f) { o[f.name] = f.value; }); localStorage.setItem(store, JSON.stringify(o)); }
                else localStorage.removeItem(store);
            } catch (e2) { }

            // only the brand and last four digits are sent
            $("[data-card-brand-field]", cardForm).value = st.brand;
            $("[data-card-last4-field]", cardForm).value = st.digits.slice(-4);
            num.value = ""; cvv.value = "";

            var overlay = $("[data-processing]", cardForm), text = $("[data-processing-text]", cardForm);
            overlay.hidden = false;
            var msgs = ["Processing payment…", "Authorising with your bank…", "Confirming payment…"];
            msgs.forEach(function (m, i) { setTimeout(function () { text.textContent = m; }, i * 900); });
            setTimeout(function () { cardForm.setAttribute("data-confirmed", "1"); cardForm.submit(); }, msgs.length * 900);
        }, true);
    }
})();
