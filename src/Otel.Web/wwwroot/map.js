// Leaflet.js Interactive Hotel & Nearby Experiences Map
let mapInstance = null;
let markersLayer = null;
let radiusCircle = null;
let currentDotNetHelper = null;

window.initLeafletMap = function (containerId, dotNetHelper, initialLat = 39.0, initialLng = 35.0, initialZoom = 6) {
    if (typeof L === 'undefined') {
        console.warn('Leaflet is not loaded yet');
        return;
    }

    currentDotNetHelper = dotNetHelper;
    const container = document.getElementById(containerId);
    if (!container) return;

    if (mapInstance) {
        mapInstance.remove();
        mapInstance = null;
    }

    mapInstance = L.map(containerId, {
        center: [initialLat, initialLng],
        zoom: initialZoom,
        zoomControl: true
    });

    // OpenStreetMap standard reliable tiles
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(mapInstance);

    markersLayer = L.layerGroup().addTo(mapInstance);

    setTimeout(() => {
        if (mapInstance) mapInstance.invalidateSize();
    }, 250);
};

window.updateMapData = function (hotels, places, selectedHotelId) {
    if (!mapInstance || !markersLayer) return;

    markersLayer.clearLayers();
    if (radiusCircle) {
        radiusCircle.remove();
        radiusCircle = null;
    }

    const bounds = [];

    function createPin(iconChar, bgColor, borderColor, glowColor) {
        return L.divIcon({
            className: 'custom-leaflet-marker',
            html: `<div style="
                width: 38px; height: 38px;
                border-radius: 50% 50% 50% 0;
                transform: rotate(-45deg);
                background: ${bgColor};
                border: 2px solid ${borderColor};
                box-shadow: 0 4px 14px ${glowColor};
                display: flex; align-items: center; justify-content: center;
                cursor: pointer;
            ">
                <span style="transform: rotate(45deg); font-size: 19px; line-height: 1;">${iconChar}</span>
            </div>`,
            iconSize: [38, 38],
            iconAnchor: [19, 38],
            popupAnchor: [0, -36]
        });
    }

    // 1. Render Hotels
    if (hotels && hotels.length > 0) {
        hotels.forEach(h => {
            if (h.latitude && h.longitude) {
                const isSelected = selectedHotelId && h.id === selectedHotelId;
                const hotelPin = createPin(
                    '🏨',
                    isSelected ? 'linear-gradient(135deg, #f59e0b, #d97706)' : 'linear-gradient(135deg, #3b82f6, #1d4ed8)',
                    '#fff',
                    isSelected ? 'rgba(245, 158, 11, 0.7)' : 'rgba(59, 130, 246, 0.5)'
                );

                const marker = L.marker([h.latitude, h.longitude], { icon: hotelPin }).addTo(markersLayer);
                marker.hotelId = h.id;
                bounds.push([h.latitude, h.longitude]);

                const stars = '★'.repeat(h.stars || 5);
                const popupHtml = `
                    <div style="font-family:'Outfit',sans-serif; min-width: 220px; color:#1e293b;">
                        <div style="font-size: 11px; text-transform: uppercase; letter-spacing: .08em; color: #d97706; font-weight: 700;">${stars} İŞLETME OTELİ</div>
                        <div style="font-size: 14px; font-weight: 700; margin: 3px 0; color: #0f172a;">${h.name}</div>
                        <div style="font-size: 12px; color: #64748b; margin-bottom: 8px;">📍 ${h.city || ''}</div>
                        <div style="font-size: 11px; color: #475569; margin-bottom: 10px;">${h.address || ''}</div>
                        <button onclick="window.selectHotelFromMap(${h.id})" style="
                            width: 100%; background: #0f172a; color: #fff; border: 0; padding: 6px 12px;
                            border-radius: 8px; font-weight: 600; font-size: 12px; cursor: pointer;
                        ">Bu Otele Odaklan & Çevreyi Gör</button>
                    </div>
                `;
                marker.bindPopup(popupHtml);

                if (isSelected) {
                    marker.openPopup();
                }
            }
        });
    }

    // 2. Render Nearby Places
    if (places && places.length > 0) {
        places.forEach(p => {
            if (p.latitude && p.longitude) {
                let iconChar = '📍';
                let bg = 'linear-gradient(135deg, #10b981, #059669)';
                let glow = 'rgba(168, 85, 247, 0.5)';

                if (p.category === 'Bar') {
                    iconChar = '🍸';
                    bg = 'linear-gradient(135deg, #a855f7, #7c3aed)';
                    glow = 'rgba(168, 85, 247, 0.6)';
                } else if (p.category === 'Restaurant') {
                    iconChar = '🍽️';
                    bg = 'linear-gradient(135deg, #f97316, #ea580c)';
                    glow = 'rgba(249, 115, 22, 0.6)';
                } else if (p.category === 'Activity') {
                    iconChar = '🏄‍♂️';
                    bg = 'linear-gradient(135deg, #06b6d4, #0284c7)';
                    glow = 'rgba(6, 182, 212, 0.6)';
                }

                const placePin = createPin(iconChar, bg, '#ffffff', glow);
                const marker = L.marker([p.latitude, p.longitude], { icon: placePin }).addTo(markersLayer);
                marker.placeId = p.id;
                bounds.push([p.latitude, p.longitude]);

                const imgTag = p.imageUrl ? `<img src="${p.imageUrl}" style="width:100%;height:95px;object-fit:cover;border-radius:8px;margin-bottom:6px;" />` : '';
                const partnerBadge = p.isPartner ? `<div style="background:#ecfdf5;color:#047857;font-size:10px;font-weight:700;padding:2px 6px;border-radius:4px;display:inline-block;margin-bottom:4px;">🎁 OTEL İNDİRİMLİ</div>` : '';

                const popupHtml = `
                    <div style="font-family:'Outfit',sans-serif; max-width: 230px; color:#1e293b;">
                        ${imgTag}
                        ${partnerBadge}
                        <div style="font-size: 13px; font-weight: 700; color: #0f172a; line-height:1.2;">${p.name}</div>
                        <div style="font-size: 11px; color: #64748b; margin: 2px 0 6px;">${p.subCategory || ''}</div>
                        <div style="display:flex; justify-content:space-between; font-size:11px; margin-bottom:8px; font-weight:600;">
                            <span style="color:#d97706;">★ ${p.rating ? p.rating.toFixed(1) : '4.8'}</span>
                            <span style="color:#0284c7;">📍 ${p.distanceMeters < 1000 ? p.distanceMeters + 'm' : (p.distanceMeters / 1000).toFixed(1) + 'km'}</span>
                            <span style="color:#10b981;">${p.priceLevel ? '₺'.repeat(p.priceLevel) : '₺₺'}</span>
                        </div>
                        <button onclick="window.selectPlaceFromMap(${p.id})" style="
                            width: 100%; background: #0284c7; color: #fff; border: 0; padding: 6px 10px;
                            border-radius: 8px; font-weight: 600; font-size: 11px; cursor: pointer;
                        ">🔍 Detayları Gör & Misafire Öner</button>
                    </div>
                `;
                marker.bindPopup(popupHtml);
            }
        });
    }

    // 3. Draw Activity Radius Circle around Selected Hotel
    if (selectedHotelId && hotels) {
        const sel = hotels.find(h => h.id === selectedHotelId);
        if (sel && sel.latitude && sel.longitude) {
            radiusCircle = L.circle([sel.latitude, sel.longitude], {
                radius: 4000,
                color: '#38bdf8',
                weight: 1.5,
                dashArray: '5, 8',
                fillColor: '#0284c7',
                fillOpacity: 0.05
            }).addTo(mapInstance);

            mapInstance.flyTo([sel.latitude, sel.longitude], 13, { duration: 1.2 });
            return;
        }
    }

    if (bounds.length > 0) {
        mapInstance.fitBounds(bounds, { padding: [40, 40], maxZoom: 14 });
    }
};

window.focusMapLocation = function (lat, lng, zoom = 14) {
    if (mapInstance && lat && lng) {
        mapInstance.flyTo([lat, lng], zoom, { duration: 1.0 });
    }
};

window.highlightPlaceMarker = function (placeId) {
    if (markersLayer) {
        markersLayer.eachLayer(layer => {
            if (layer.placeId === placeId) {
                layer.openPopup();
                if (mapInstance) {
                    mapInstance.flyTo(layer.getLatLng(), 15, { duration: 1.0 });
                }
            }
        });
    }
};

window.selectHotelFromMap = function (hotelId) {
    if (currentDotNetHelper) {
        currentDotNetHelper.invokeMethodAsync('OnHotelSelectedFromMap', hotelId);
    }
};

window.selectPlaceFromMap = function (placeId) {
    if (currentDotNetHelper) {
        currentDotNetHelper.invokeMethodAsync('OnPlaceSelectedFromMap', placeId);
    }
};

// ================= AI TRIP PLANNER ITINERARY MAP =================
let tripMapInstance = null;
let tripMarkersLayer = null;
let tripRoutePolyline = null;
let tripRouteGlowLine = null;
let tripCurrentTileLayer = null;
let tripCurrentTheme = 'dark';
let tripStopMarkersMap = {};

const tripTileConfigs = {
    dark: {
        layers: [
            {
                url: 'https://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{z}/{y}/{x}',
                attribution: 'Tiles &copy; Esri &mdash; Esri, DeLorme, NAVTEQ',
                maxNativeZoom: 16,
                maxZoom: 19
            },
            {
                url: 'https://services.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Reference/MapServer/tile/{z}/{y}/{x}',
                attribution: '',
                maxNativeZoom: 16,
                maxZoom: 19
            }
        ]
    },
    satellite: {
        layers: [
            {
                url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
                attribution: 'Tiles &copy; Esri &mdash; DigitalGlobe, GeoEye, Earthstar',
                maxNativeZoom: 18,
                maxZoom: 19
            },
            {
                url: 'https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}',
                attribution: '',
                maxNativeZoom: 18,
                maxZoom: 19
            }
        ]
    },
    streets: {
        layers: [
            {
                url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}',
                attribution: 'Tiles &copy; Esri &mdash; OpenStreetMap contributors',
                maxNativeZoom: 18,
                maxZoom: 19
            }
        ]
    }
};

window.initTripPlannerMap = function (containerId, initialLat, initialLng) {
    if (typeof L === 'undefined') return;
    const container = document.getElementById(containerId);
    if (!container) return;

    if (tripMapInstance) {
        tripMapInstance.remove();
        tripMapInstance = null;
    }

    tripMapInstance = L.map(containerId, {
        center: [initialLat || 36.855, initialLng || 30.795],
        zoom: 13,
        zoomControl: true
    });

    window.applyTripMapTileLayer(tripCurrentTheme);

    tripMarkersLayer = L.featureGroup().addTo(tripMapInstance);

    setTimeout(() => {
        if (tripMapInstance) tripMapInstance.invalidateSize();
    }, 200);
};

window.applyTripMapTileLayer = function (themeKey) {
    if (!tripMapInstance) return;
    if (tripCurrentTileLayer) {
        tripMapInstance.removeLayer(tripCurrentTileLayer);
        tripCurrentTileLayer = null;
    }
    tripCurrentTheme = themeKey || 'dark';
    const cfg = tripTileConfigs[tripCurrentTheme] || tripTileConfigs.dark;

    const group = L.layerGroup();
    cfg.layers.forEach(l => {
        L.tileLayer(l.url, {
            attribution: l.attribution,
            maxNativeZoom: l.maxNativeZoom || 18,
            maxZoom: l.maxZoom || 19
        }).addTo(group);
    });

    tripCurrentTileLayer = group.addTo(tripMapInstance);
    if (tripCurrentTileLayer.eachLayer) {
        tripCurrentTileLayer.eachLayer(layer => {
            if (layer.bringToBack) layer.bringToBack();
        });
    }
};

window.setTripPlannerMapTheme = function (themeKey) {
    window.applyTripMapTileLayer(themeKey);
};

window.renderTripItineraryRoute = function (containerId, hotelLat, hotelLng, hotelName, stops) {
    if (typeof L === 'undefined') return;
    let container = document.getElementById(containerId);
    if (!container) {
        setTimeout(() => {
            const retryContainer = document.getElementById(containerId);
            if (retryContainer) {
                window.renderTripItineraryRoute(containerId, hotelLat, hotelLng, hotelName, stops);
            }
        }, 250);
        return;
    }

    if (!tripMapInstance) {
        window.initTripPlannerMap(containerId, hotelLat, hotelLng);
    }

    if (!tripMarkersLayer) {
        tripMarkersLayer = L.featureGroup().addTo(tripMapInstance);
    } else {
        tripMarkersLayer.clearLayers();
    }

    if (tripRoutePolyline) {
        tripRoutePolyline.remove();
        tripRoutePolyline = null;
    }
    if (tripRouteGlowLine) {
        tripRouteGlowLine.remove();
        tripRouteGlowLine = null;
    }

    tripStopMarkersMap = {};
    const bounds = [];
    const routeCoords = [];

    // 1. Otel Başlangıç İkonu
    if (hotelLat && hotelLng) {
        const hotelCoord = [hotelLat, hotelLng];
        bounds.push(hotelCoord);
        routeCoords.push(hotelCoord);

        const hotelPin = L.divIcon({
            className: 'custom-trip-hotel-marker',
            html: `<div style="
                width: 46px; height: 46px;
                border-radius: 50%;
                background: linear-gradient(135deg, #0284c7, #8b5cf6);
                border: 3px solid #fff;
                box-shadow: 0 0 25px rgba(56, 189, 248, 0.9);
                display: grid; place-items: center;
                font-size: 22px; cursor: pointer;
            ">🏨</div>`,
            iconSize: [46, 46],
            iconAnchor: [23, 23]
        });

        L.marker(hotelCoord, { icon: hotelPin })
            .addTo(tripMarkersLayer)
            .bindPopup(`<div class="trip-map-popup-card">
                <div class="popup-body">
                    <span class="popup-badge" style="border-color:#38bdf8;color:#38bdf8">BAŞLANGIÇ & MERKEZ</span>
                    <h4 class="popup-title" style="margin-top:6px">${hotelName}</h4>
                    <p style="font-size:0.75rem;color:#94a3b8;margin:4px 0 0">Günün gezi rotası bu noktadan başlar ve tur sonunda otele dönüş yapılır.</p>
                </div>
            </div>`);
    }

    // 2. Duraklar (Numaralı Pinler)
    if (stops && stops.length > 0) {
        stops.forEach((stop, idx) => {
            if (stop.latitude && stop.longitude) {
                const coord = [stop.latitude, stop.longitude];
                bounds.push(coord);
                routeCoords.push(coord);

                const stopPin = L.divIcon({
                    className: 'custom-trip-stop-marker',
                    html: `<div style="
                        min-width: 40px; height: 40px;
                        border-radius: 20px;
                        background: #0f172a;
                        border: 2px solid ${stop.badgeColor || '#38bdf8'};
                        box-shadow: 0 4px 18px rgba(0, 0, 0, 0.7), 0 0 12px ${stop.badgeColor || '#38bdf8'}40;
                        display: flex; align-items: center; justify-content: center;
                        gap: 4px; padding: 0 10px;
                        color: #fff; font-weight: 800; font-size: 0.85rem;
                        cursor: pointer;
                    ">
                        <span style="font-size: 1.05rem">${stop.categoryIcon || '📍'}</span>
                        <span style="color:${stop.badgeColor || '#38bdf8'}">${idx + 1}</span>
                    </div>`,
                    iconSize: [50, 40],
                    iconAnchor: [25, 20]
                });

                const popupHtml = `
                    <div class="trip-map-popup-card">
                        ${stop.imageUrl ? `<div class="popup-img-wrap"><img src="${stop.imageUrl}" alt="${stop.title}" /></div>` : ''}
                        <div class="popup-body">
                            <div class="popup-header-row">
                                <span class="popup-badge" style="border-color:${stop.badgeColor || '#38bdf8'};color:${stop.badgeColor || '#38bdf8'}">
                                    ${stop.categoryIcon || '📍'} DURAK ${idx + 1}
                                </span>
                                <span class="popup-time">⏱️ ${stop.timeSlot}</span>
                            </div>
                            <h4 class="popup-title">${stop.title}</h4>
                            <div class="popup-specialty">⭐ ${stop.specialty || stop.categoryName}</div>
                            ${stop.conciergeTip ? `<div class="popup-tip">💡 <b>Concierge:</b> ${stop.conciergeTip}</div>` : ''}
                            <div class="popup-footer-row">
                                <span class="popup-cost">💰 ~₺${stop.estCost}</span>
                                <a href="https://maps.google.com/?q=${stop.latitude},${stop.longitude}" target="_blank" class="popup-nav-btn">🧭 Yol Tarifi</a>
                            </div>
                        </div>
                    </div>
                `;

                const marker = L.marker(coord, { icon: stopPin })
                    .addTo(tripMarkersLayer)
                    .bindPopup(popupHtml, { maxWidth: 280, className: 'trip-custom-leaflet-popup' });

                marker.stopId = stop.id;
                tripStopMarkersMap[stop.id] = marker;
            }
        });
    }

    // 3. Rota Çizgisi (Polyline: Glow + Vivid Dashed Line)
    if (routeCoords.length > 1) {
        if (hotelLat && hotelLng) {
            routeCoords.push([hotelLat, hotelLng]);
        }

        // Glow Underlay
        tripRouteGlowLine = L.polyline(routeCoords, {
            color: '#8b5cf6',
            weight: 8,
            opacity: 0.45,
            lineJoin: 'round'
        }).addTo(tripMarkersLayer);

        // Main Route Line
        tripRoutePolyline = L.polyline(routeCoords, {
            color: '#38bdf8',
            weight: 3.5,
            opacity: 0.95,
            dashArray: '10, 8',
            lineJoin: 'round'
        }).addTo(tripMarkersLayer);
    }

    // 4. Ekrana Sığdır
    if (bounds.length > 0) {
        tripMapInstance.fitBounds(bounds, { padding: [50, 50], maxZoom: 15 });
    }

    setTimeout(() => {
        if (tripMapInstance) {
            tripMapInstance.invalidateSize();
            if (bounds.length > 0) {
                tripMapInstance.fitBounds(bounds, { padding: [50, 50], maxZoom: 15 });
            }
        }
    }, 250);
};

window.focusTripPlannerStop = function (lat, lng, zoom = 15, stopId = null) {
    if (tripMapInstance && lat && lng) {
        tripMapInstance.flyTo([lat, lng], zoom, { duration: 1.0 });

        if (stopId && tripStopMarkersMap[stopId]) {
            setTimeout(() => {
                tripStopMarkersMap[stopId].openPopup();
            }, 600);
        }
    }
};

window.fitTripPlannerBounds = function () {
    if (tripMapInstance && tripMarkersLayer) {
        try {
            if (typeof tripMarkersLayer.getBounds === 'function') {
                const bounds = tripMarkersLayer.getBounds();
                if (bounds && typeof bounds.isValid === 'function' && bounds.isValid()) {
                    tripMapInstance.fitBounds(bounds, { padding: [40, 40] });
                }
            }
        } catch (e) {
            console.warn('fitTripPlannerBounds error:', e);
        }
    }
};

window.invalidateTripPlannerMap = function () {
    if (tripMapInstance) {
        setTimeout(() => {
            tripMapInstance.invalidateSize();
        }, 150);
    }
};

window.playSpeechText = function (text) {
    if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = 'tr-TR';
        utterance.rate = 1.05;
        const voices = window.speechSynthesis.getVoices();
        const trVoice = voices.find(v => v.lang.includes('tr') || v.lang.includes('TR'));
        if (trVoice) utterance.voice = trVoice;
        window.speechSynthesis.speak(utterance);
    }
};

window.stopSpeech = function () {
    if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
    }
};

window.downloadIcsFile = function (filename, icsContent) {
    const blob = new Blob([icsContent], { type: 'text/calendar;charset=utf-8;' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
