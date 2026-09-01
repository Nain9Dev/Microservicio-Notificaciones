/* ==========================================================================
   NainDev Cloud Notification Console
   Drives the live view from real pipeline telemetry delivered over SSE.
   No framework, no bundler, no external dependency.
   ========================================================================== */
(function () {
    "use strict";

    // ----------------------------------------------------------------------
    // Configuration
    // ----------------------------------------------------------------------
    var API = "api/v1/notifications";
    var MAX_STREAM_ROWS = 120;
    var PREVIEW_DEBOUNCE_MS = 420;
    var HEALTH_POLL_MS = 20000;
    var BURST_SIZE = 5;
    var BURST_GAP_MS = 260;

    var STAGE_COLOR = {
        Accepted: "#7DD3FC",
        Queued: "#FBBF24",
        Received: "#A78BFA",
        Rendered: "#F0ABFC",
        Dispatched: "#34D399",
        Failed: "#FB7185"
    };

    // Stage -> which wire lights up and which node takes the spotlight.
    var STAGE_FLOW = {
        Accepted: { wire: "1", node: "gateway", counters: ["client", "gateway"] },
        Queued: { wire: "2", node: "broker", counters: ["broker"] },
        Received: { wire: "3", node: "worker", counters: ["worker"] },
        Rendered: { wire: null, node: "worker", counters: [] },
        Dispatched: { wire: "4", node: "delivery", counters: ["delivery"] },
        Failed: { wire: null, node: null, counters: [] }
    };

    var TYPE_COLOR = {
        ContactFormSubmission: "#38BDF8",
        WelcomeMessage: "#34D399",
        SystemAlert: "#FB7185",
        TestDemo: "#A78BFA"
    };

    var CHANNEL_COLOR = {
        smtp: "#34D399",
        simulation: "#FBBF24",
        webhook: "#A78BFA"
    };

    var prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // ----------------------------------------------------------------------
    // Small helpers
    // ----------------------------------------------------------------------
    function $(id) { return document.getElementById(id); }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined && text !== null) { node.textContent = String(text); }
        return node;
    }

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function formatBytes(bytes) {
        if (!bytes) { return { value: 0, unit: "KB" }; }
        if (bytes < 1024 * 1024) { return { value: Math.round(bytes / 1024), unit: "KB" }; }
        return { value: Math.round((bytes / (1024 * 1024)) * 10) / 10, unit: "MB" };
    }

    function formatUptime(seconds) {
        if (!seconds || seconds < 0) { return "0s"; }
        var h = Math.floor(seconds / 3600);
        var m = Math.floor((seconds % 3600) / 60);
        var s = Math.floor(seconds % 60);
        if (h > 0) { return h + "h " + m + "m"; }
        if (m > 0) { return m + "m " + s + "s"; }
        return s + "s";
    }

    function formatClock(isoString) {
        var date = isoString ? new Date(isoString) : new Date();
        if (isNaN(date.getTime())) { date = new Date(); }
        return date.toLocaleTimeString("es-ES", { hour12: false });
    }

    // ----------------------------------------------------------------------
    // Animated numeric readouts
    // ----------------------------------------------------------------------
    var counterState = {};
    var counterTokens = {};

    function setNumber(node, target, suffix) {
        if (!node) { return; }

        var key = node.id;
        var current = counterState[key];
        if (current === undefined) { current = 0; }

        target = Number(target) || 0;

        if (current === target) { return; }

        if (target > current) {
            node.classList.remove("is-bumping");
            // Force a reflow so the animation restarts on consecutive updates.
            void node.offsetWidth;
            node.classList.add("is-bumping");
        }

        // Browsers suspend requestAnimationFrame on hidden tabs, so an animated update would
        // leave the readout frozen on a stale value. Paint it straight away instead.
        if (prefersReducedMotion || document.hidden) {
            counterTokens[key] = (counterTokens[key] || 0) + 1;
            counterState[key] = target;
            paintNumber(node, target, suffix);
            return;
        }

        // A token invalidates any animation still running for this readout, so overlapping
        // updates never fight over the same element.
        var token = (counterTokens[key] || 0) + 1;
        counterTokens[key] = token;

        var start = current;
        var startedAt = performance.now();
        var duration = 520;

        function step(now) {
            if (counterTokens[key] !== token) { return; }

            var progress = clamp((now - startedAt) / duration, 0, 1);
            // Ease-out cubic keeps the last digits from crawling.
            var eased = 1 - Math.pow(1 - progress, 3);
            var value = progress === 1 ? target : start + (target - start) * eased;

            counterState[key] = value;
            paintNumber(node, value, suffix);

            if (progress < 1) { requestAnimationFrame(step); }
        }

        requestAnimationFrame(step);
    }

    function paintNumber(node, value, suffix) {
        var rounded = Math.abs(value) >= 100 || Number.isInteger(value)
            ? Math.round(value)
            : Math.round(value * 10) / 10;

        node.textContent = String(rounded);

        if (suffix) {
            node.appendChild(el("small", null, suffix));
        }
    }

    // ----------------------------------------------------------------------
    // Pipeline animation
    // ----------------------------------------------------------------------
    var nodeTimers = {};
    var nodeCounters = { client: 0, gateway: 0, broker: 0, worker: 0, delivery: 0 };

    function pulseNode(nodeName, color) {
        if (!nodeName) { return; }

        var node = document.querySelector('.node[data-node="' + nodeName + '"]');
        if (!node) { return; }

        node.style.setProperty("--node-accent", color);
        node.classList.add("is-active");

        if (nodeTimers[nodeName]) { clearTimeout(nodeTimers[nodeName]); }

        nodeTimers[nodeName] = setTimeout(function () {
            node.classList.remove("is-active");
        }, 1100);
    }

    function chargeWire(wireId, color) {
        if (!wireId) { return; }

        var wire = document.querySelector('.wire[data-wire="' + wireId + '"]');
        if (!wire) { return; }

        wire.classList.remove("is-charged");
        void wire.offsetWidth;
        wire.classList.add("is-charged");

        if (prefersReducedMotion) { return; }

        // One packet element per event so concurrent notifications overlap naturally.
        var packet = el("span", "packet");
        packet.style.setProperty("--packet-color", color);
        wire.appendChild(packet);

        packet.addEventListener("animationend", function () {
            if (packet.parentNode) { packet.parentNode.removeChild(packet); }
        });
    }

    function bumpNodeCounters(names) {
        names.forEach(function (name) {
            nodeCounters[name] = (nodeCounters[name] || 0) + 1;

            var badge = document.querySelector('.node-counter[data-counter="' + name + '"]');
            if (badge) { badge.textContent = String(nodeCounters[name]); }
        });
    }

    function animateStage(lifecycleEvent) {
        var stage = lifecycleEvent.stage;
        var flow = STAGE_FLOW[stage];
        var color = STAGE_COLOR[stage] || "#38BDF8";

        if (stage === "Failed") {
            pulseNode(lifecycleEvent.source === "gateway" ? "gateway" : "worker", color);
            return;
        }

        if (!flow) { return; }

        chargeWire(flow.wire, color);
        pulseNode(flow.node, color);
        bumpNodeCounters(flow.counters);
    }

    // ----------------------------------------------------------------------
    // Event stream log
    // ----------------------------------------------------------------------
    var streamRoot = $("eventStream");
    var streamEmpty = $("streamEmpty");

    function appendStreamRow(lifecycleEvent) {
        if (streamEmpty && streamEmpty.parentNode) {
            streamEmpty.parentNode.removeChild(streamEmpty);
            streamEmpty = null;
        }

        var row = el("div", "stream-row");
        row.setAttribute("data-stage", lifecycleEvent.stage);

        row.appendChild(el("span", "row-time", formatClock(lifecycleEvent.timestamp)));
        row.appendChild(el("span", "row-stage", lifecycleEvent.stage));

        var detail = el("div", "row-detail");
        detail.appendChild(document.createTextNode(
            lifecycleEvent.error ? lifecycleEvent.error : (lifecycleEvent.detail || lifecycleEvent.subject || "")));

        var meta = lifecycleEvent.type + " · " + String(lifecycleEvent.notificationId).slice(0, 8);
        if (lifecycleEvent.recipient) { meta += " · " + lifecycleEvent.recipient; }
        if (lifecycleEvent.attempt > 1) { meta += " · intento " + lifecycleEvent.attempt; }
        detail.appendChild(el("small", null, meta));

        row.appendChild(detail);

        var elapsed = Math.round(lifecycleEvent.elapsedMilliseconds || 0);
        row.appendChild(el("span", "row-elapsed", elapsed + " ms"));

        // Newest first: always insert at the top and trim the tail.
        streamRoot.insertBefore(row, streamRoot.firstChild);

        while (streamRoot.children.length > MAX_STREAM_ROWS) {
            streamRoot.removeChild(streamRoot.lastChild);
        }
    }

    // ----------------------------------------------------------------------
    // Metrics rendering
    // ----------------------------------------------------------------------
    var chartCanvas = $("throughputChart");
    var chartContext = chartCanvas ? chartCanvas.getContext("2d") : null;
    var lastSeries = [];

    function renderSnapshot(snapshot, listeners) {
        if (!snapshot) { return; }

        setNumber($("kpiDispatched"), snapshot.dispatched);
        setNumber($("kpiLatency"), snapshot.p95LatencyMs, "ms");
        setNumber($("kpiSuccess"), snapshot.successRate, "%");

        var bytes = formatBytes(snapshot.renderedBytes);
        setNumber($("kpiBytes"), bytes.value, bytes.unit);

        $("kpiInFlight").textContent = String(snapshot.inFlight || 0);
        $("kpiFailed").textContent = String(snapshot.failed || 0);
        $("kpiAvgLatency").textContent = String(Math.round(snapshot.averageLatencyMs || 0));

        $("footUptime").textContent = formatUptime(snapshot.uptimeSeconds);
        $("footFastest").textContent = snapshot.fastestLatencyMs
            ? Math.round(snapshot.fastestLatencyMs) + " ms"
            : "—";

        if (listeners !== undefined && listeners !== null) {
            $("footListeners").textContent = String(listeners);
        }

        lastSeries = snapshot.throughputSeries || [];
        drawThroughput(lastSeries);

        renderBreakdown($("typeBreakdown"), snapshot.byType, TYPE_COLOR, "#38BDF8");
        renderBreakdown($("channelBreakdown"), snapshot.byChannel, CHANNEL_COLOR, "#34D399");
    }

    function renderBreakdown(list, counters, palette, fallbackColor) {
        if (!list) { return; }

        var keys = counters ? Object.keys(counters) : [];

        list.textContent = "";

        if (keys.length === 0) {
            list.appendChild(el("li", "breakdown-empty", "Sin datos todavía"));
            return;
        }

        var max = 0;
        keys.forEach(function (key) { max = Math.max(max, counters[key]); });

        keys.sort(function (a, b) { return counters[b] - counters[a]; });

        keys.forEach(function (key) {
            var item = el("li", "breakdown-item");
            item.appendChild(el("span", "breakdown-name", key));
            item.appendChild(el("span", "breakdown-count", counters[key]));

            var bar = el("span", "breakdown-bar");
            var fill = el("span", "breakdown-fill");
            fill.style.setProperty("--bar-color", palette[key] || fallbackColor);
            fill.style.width = (max > 0 ? (counters[key] / max) * 100 : 0) + "%";
            bar.appendChild(fill);
            item.appendChild(bar);

            list.appendChild(item);
        });
    }

    function drawThroughput(series) {
        if (!chartContext || !chartCanvas) { return; }

        var dpr = window.devicePixelRatio || 1;
        var width = chartCanvas.clientWidth;
        var height = chartCanvas.clientHeight || 90;

        if (width === 0) { return; }

        chartCanvas.width = Math.round(width * dpr);
        chartCanvas.height = Math.round(height * dpr);
        chartContext.setTransform(dpr, 0, 0, dpr, 0, 0);
        chartContext.clearRect(0, 0, width, height);

        var points = series && series.length ? series : new Array(60).fill(0);
        var peak = 0;
        points.forEach(function (value) { peak = Math.max(peak, value); });

        var peakLabel = $("throughputPeak");
        if (peakLabel) { peakLabel.textContent = "pico " + peak + "/s"; }

        var scaleMax = Math.max(peak, 4);
        var stepX = width / (points.length - 1);
        var padding = 6;
        var usableHeight = height - padding * 2;

        // Baseline grid.
        chartContext.strokeStyle = "rgba(148, 163, 184, 0.12)";
        chartContext.lineWidth = 1;
        for (var g = 0; g <= 2; g++) {
            var gy = padding + (usableHeight / 2) * g;
            chartContext.beginPath();
            chartContext.moveTo(0, gy);
            chartContext.lineTo(width, gy);
            chartContext.stroke();
        }

        function pointY(value) {
            return padding + usableHeight - (value / scaleMax) * usableHeight;
        }

        // Filled area.
        var gradient = chartContext.createLinearGradient(0, 0, 0, height);
        gradient.addColorStop(0, "rgba(56, 189, 248, 0.34)");
        gradient.addColorStop(1, "rgba(56, 189, 248, 0)");

        chartContext.beginPath();
        chartContext.moveTo(0, height);
        points.forEach(function (value, index) {
            chartContext.lineTo(index * stepX, pointY(value));
        });
        chartContext.lineTo(width, height);
        chartContext.closePath();
        chartContext.fillStyle = gradient;
        chartContext.fill();

        // Line on top.
        chartContext.beginPath();
        points.forEach(function (value, index) {
            var x = index * stepX;
            var y = pointY(value);
            if (index === 0) { chartContext.moveTo(x, y); } else { chartContext.lineTo(x, y); }
        });
        chartContext.strokeStyle = "#38BDF8";
        chartContext.lineWidth = 1.8;
        chartContext.lineJoin = "round";
        chartContext.stroke();

        // Highlight the most recent sample.
        var lastValue = points[points.length - 1];
        if (lastValue > 0) {
            chartContext.beginPath();
            chartContext.arc(width - 1, pointY(lastValue), 3, 0, Math.PI * 2);
            chartContext.fillStyle = "#38BDF8";
            chartContext.fill();
        }
    }

    // ----------------------------------------------------------------------
    // Live connection (Server-Sent Events)
    // ----------------------------------------------------------------------
    var statusDot = document.querySelector("#statusPill .status-dot");
    var statusLabel = $("statusLabel");
    var statusMeta = $("statusMeta");
    var source = null;
    var reconnectDelay = 1500;

    function setStatus(state, label, meta) {
        if (statusDot) { statusDot.setAttribute("data-state", state); }
        if (statusLabel) { statusLabel.textContent = label; }
        if (meta !== undefined && statusMeta) { statusMeta.textContent = meta; }
    }

    function connectStream() {
        if (source) { source.close(); }

        setStatus("connecting", "Conectando", "sse");

        source = new EventSource(API + "/stream");

        source.addEventListener("open", function () {
            reconnectDelay = 1500;
            setStatus("live", "En vivo", "sse");
        });

        source.addEventListener("hydrate", function (event) {
            var payload = parse(event.data);
            if (!payload) { return; }

            renderSnapshot(payload.snapshot, payload.listeners);

            // Recent events arrive newest first; replay them oldest first so the log reads naturally.
            var recent = payload.recent || [];
            for (var i = recent.length - 1; i >= 0; i--) {
                appendStreamRow(recent[i]);
            }
        });

        source.addEventListener("lifecycle", function (event) {
            var lifecycleEvent = parse(event.data);
            if (!lifecycleEvent) { return; }

            animateStage(lifecycleEvent);
            appendStreamRow(lifecycleEvent);
        });

        source.addEventListener("metrics", function (event) {
            var snapshot = parse(event.data);
            if (snapshot) { renderSnapshot(snapshot, null); }
        });

        source.addEventListener("error", function () {
            setStatus("down", "Reconectando", "sse");

            // EventSource retries on its own unless the connection is definitively closed.
            if (source.readyState === EventSource.CLOSED) {
                reconnectDelay = Math.min(reconnectDelay * 2, 20000);
                setTimeout(connectStream, reconnectDelay);
            }
        });
    }

    function parse(raw) {
        try {
            return JSON.parse(raw);
        } catch (error) {
            return null;
        }
    }

    // ----------------------------------------------------------------------
    // Health polling: reports the real transport behind the gateway
    // ----------------------------------------------------------------------
    function pollHealth() {
        fetch(API + "/health", { headers: { Accept: "application/json" } })
            .then(function (response) { return response.json(); })
            .then(function (health) {
                var transport = health.transport === "in-memory" ? "In-memory" : "RabbitMQ";

                $("footTransport").textContent = transport +
                    (health.workerInProcess ? " · worker en proceso" : " · worker externo");

                var brokerTech = $("brokerTech");
                if (brokerTech) {
                    brokerTech.textContent = health.transport === "in-memory" ? "MassTransit" : "RabbitMQ";
                }

                if (health.status === "Healthy") {
                    setStatus(source && source.readyState === EventSource.OPEN ? "live" : "connecting",
                        source && source.readyState === EventSource.OPEN ? "En vivo" : "Conectando",
                        transport.toLowerCase());
                } else {
                    setStatus("down", health.status === "Degraded" ? "Degradado" : "Broker caído", transport.toLowerCase());
                }
            })
            .catch(function () {
                setStatus("down", "Gateway sin respuesta", "offline");
            });
    }

    // ----------------------------------------------------------------------
    // Playground: dispatch and preview
    // ----------------------------------------------------------------------
    var form = $("dispatchForm");
    var feedback = $("formFeedback");
    var dispatchButton = $("dispatchButton");
    var burstButton = $("burstButton");
    var previewFrame = $("previewFrame");
    var previewText = $("previewText");
    var previewLoading = $("previewLoading");
    var previewController = null;
    var previewTimer = null;

    function readForm() {
        var checked = form.querySelector('input[name="eventType"]:checked');

        return {
            targetRecipient: $("targetRecipient").value.trim(),
            subject: $("subject").value.trim(),
            content: $("content").value,
            eventType: checked ? checked.value : "TestDemo",
            priority: $("priority").value,
            senderName: $("senderName").value.trim()
        };
    }

    function setFeedback(message, kind) {
        if (!feedback) { return; }
        feedback.className = "form-feedback" + (kind ? " is-" + kind : "");
        feedback.innerHTML = message;
    }

    function validate(payload) {
        if (!payload.subject) { return "El asunto es obligatorio."; }
        if (!payload.content || payload.content.trim().length === 0) { return "El mensaje no puede estar vacío."; }
        if (!payload.targetRecipient || payload.targetRecipient.indexOf("@") < 1) {
            return "Introduce un destinatario válido.";
        }
        return null;
    }

    function schedulePreview() {
        if (previewTimer) { clearTimeout(previewTimer); }
        previewTimer = setTimeout(refreshPreview, PREVIEW_DEBOUNCE_MS);
    }

    function refreshPreview() {
        var payload = readForm();

        if (validate(payload)) { return; }

        if (previewController) { previewController.abort(); }
        previewController = new AbortController();

        if (previewLoading) { previewLoading.hidden = false; }

        fetch(API + "/preview", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
            signal: previewController.signal
        })
            .then(function (response) {
                if (!response.ok) { throw new Error("preview failed"); }
                return response.json();
            })
            .then(function (result) {
                // The iframe is fully sandboxed: unique origin, scripts disabled.
                previewFrame.srcdoc = result.html;
                previewText.textContent = result.plainText;

                $("previewBytes").textContent = String(result.bytes);
                $("previewTime").textContent = String(result.renderMilliseconds);
                $("previewSubject").textContent = payload.subject || "—";

                if (previewLoading) { previewLoading.hidden = true; }
            })
            .catch(function (error) {
                if (error.name === "AbortError") { return; }
                if (previewLoading) { previewLoading.hidden = true; }
            });
    }

    function dispatchOne(payload) {
        return fetch(API + "/demo", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        }).then(function (response) {
            return response.json().then(function (body) {
                return { ok: response.ok, status: response.status, body: body };
            });
        });
    }

    function describeResult(result) {
        if (result.status === 429) {
            setFeedback("Límite de peticiones alcanzado. El gateway aplica rate limiting por IP.", "warn");
            return;
        }

        if (result.status === 503) {
            setFeedback("El broker no está disponible: " + (result.body.detail || "sin detalle") + ".", "error");
            return;
        }

        if (!result.ok) {
            var reason = result.body && result.body.title ? result.body.title : "Petición rechazada";
            setFeedback(reason + ".", "error");
            return;
        }

        var message = "Encolado con tracking <code>" +
            String(result.body.assignedTrackingId).slice(0, 8) + "</code>";

        if (result.body.sandboxed) {
            message += " · destinatario redirigido al buzón de pruebas";
            setFeedback(message, "warn");
            return;
        }

        setFeedback(message, "ok");
    }

    function setBusy(isBusy) {
        if (!dispatchButton) { return; }
        dispatchButton.disabled = isBusy;
        dispatchButton.classList.toggle("is-busy", isBusy);
        if (burstButton) { burstButton.disabled = isBusy; }
    }

    if (form) {
        form.addEventListener("submit", function (event) {
            event.preventDefault();

            var payload = readForm();
            var problem = validate(payload);

            if (problem) {
                setFeedback(problem, "error");
                return;
            }

            setBusy(true);
            setFeedback("Publicando en el broker…", null);

            dispatchOne(payload)
                .then(describeResult)
                .catch(function () {
                    setFeedback("No se pudo contactar con el gateway.", "error");
                })
                .then(function () { setBusy(false); });
        });

        form.addEventListener("input", schedulePreview);
        form.addEventListener("change", schedulePreview);
    }

    if (burstButton) {
        burstButton.addEventListener("click", function () {
            var payload = readForm();
            var problem = validate(payload);

            if (problem) {
                setFeedback(problem, "error");
                return;
            }

            setBusy(true);
            setFeedback("Lanzando ráfaga de " + BURST_SIZE + " eventos…", null);

            var sent = 0;
            var rejected = 0;

            function next(index) {
                if (index >= BURST_SIZE) {
                    setBusy(false);
                    setFeedback(
                        sent + " eventos encolados" + (rejected > 0 ? ", " + rejected + " rechazados por rate limiting" : "") + ".",
                        rejected > 0 ? "warn" : "ok");
                    return;
                }

                var burstPayload = Object.assign({}, payload, {
                    subject: payload.subject + " #" + (index + 1)
                });

                dispatchOne(burstPayload)
                    .then(function (result) {
                        if (result.ok) { sent++; } else { rejected++; }
                    })
                    .catch(function () { rejected++; })
                    .then(function () {
                        setTimeout(function () { next(index + 1); }, BURST_GAP_MS);
                    });
            }

            next(0);
        });
    }

    // Preview format tabs -----------------------------------------------------
    Array.prototype.forEach.call(document.querySelectorAll(".tab"), function (tab) {
        tab.addEventListener("click", function () {
            Array.prototype.forEach.call(document.querySelectorAll(".tab"), function (other) {
                other.classList.remove("is-active");
                other.setAttribute("aria-selected", "false");
            });

            tab.classList.add("is-active");
            tab.setAttribute("aria-selected", "true");

            var showText = tab.getAttribute("data-tab") === "text";
            previewFrame.hidden = showText;
            previewText.hidden = !showText;
        });
    });

    // Character counter -------------------------------------------------------
    var contentField = $("content");
    var contentCounter = $("contentCounter");

    function updateCounter() {
        if (contentCounter && contentField) {
            contentCounter.textContent = contentField.value.length + " / 5000";
        }
    }

    if (contentField) {
        contentField.addEventListener("input", updateCounter);
        updateCounter();
    }

    // Clear the stream log ----------------------------------------------------
    var clearButton = $("clearStream");
    if (clearButton) {
        clearButton.addEventListener("click", function () {
            streamRoot.textContent = "";
            streamEmpty = el("div", "stream-empty", "Registro limpiado. Los nuevos eventos aparecerán aquí.");
            streamRoot.appendChild(streamEmpty);
        });
    }

    // Smooth scroll shortcuts -------------------------------------------------
    Array.prototype.forEach.call(document.querySelectorAll("[data-scroll-to]"), function (button) {
        button.addEventListener("click", function () {
            var target = $(button.getAttribute("data-scroll-to"));
            if (target) { target.scrollIntoView({ behavior: prefersReducedMotion ? "auto" : "smooth", block: "start" }); }
        });
    });

    // Keep the chart crisp on resize -----------------------------------------
    var resizeTimer = null;
    window.addEventListener("resize", function () {
        if (resizeTimer) { clearTimeout(resizeTimer); }
        resizeTimer = setTimeout(function () { drawThroughput(lastSeries); }, 160);
    });

    // ----------------------------------------------------------------------
    // Boot
    // ----------------------------------------------------------------------
    connectStream();
    pollHealth();
    setInterval(pollHealth, HEALTH_POLL_MS);
    refreshPreview();
    drawThroughput([]);
})();
