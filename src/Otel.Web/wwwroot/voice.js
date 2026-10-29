// OtelPro Sesli Yapay Zeka (Speech-to-Text & Text-to-Speech - Akıllı Sesli Asistan)
window.OtelVoice = {
    recognition: null,
    isListening: false,
    lastTranscript: '',
    cachedVoices: [],

    initVoices: function () {
        if (!window.speechSynthesis) return;
        this.cachedVoices = window.speechSynthesis.getVoices() || [];
        if (window.speechSynthesis.onvoiceschanged !== undefined) {
            var self = this;
            window.speechSynthesis.onvoiceschanged = function () {
                self.cachedVoices = window.speechSynthesis.getVoices() || [];
            };
        }
    },

    startListening: function (dotNetHelper, lang) {
        var SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            dotNetHelper.invokeMethodAsync('OnSpeechError', 'Tarayıcınız ses tanıma (Web Speech API) özelliğini desteklemiyor. Lütfen Google Chrome veya Microsoft Edge kullanın.');
            return false;
        }

        try {
            if (this.recognition) {
                try { this.recognition.abort(); } catch (e) { }
            }

            var rec = new SpeechRecognition();
            rec.lang = lang || 'tr-TR';
            rec.continuous = false;
            rec.interimResults = true;
            rec.maxAlternatives = 1;

            this.lastTranscript = '';
            var self = this;

            rec.onstart = function () {
                self.isListening = true;
                dotNetHelper.invokeMethodAsync('OnSpeechStart');
            };

            rec.onresult = function (event) {
                var interim = '';
                var finalPart = '';
                for (var i = event.resultIndex; i < event.results.length; ++i) {
                    if (event.results[i].isFinal) {
                        finalPart += event.results[i][0].transcript;
                    } else {
                        interim += event.results[i][0].transcript;
                    }
                }
                var current = finalPart || interim;
                if (current && current.trim()) {
                    self.lastTranscript = current.trim();
                }
                var isFinal = event.results[event.results.length - 1].isFinal;
                dotNetHelper.invokeMethodAsync('OnSpeechResult', self.lastTranscript, isFinal);
            };

            rec.onerror = function (event) {
                self.isListening = false;
                dotNetHelper.invokeMethodAsync('OnSpeechError', event.error || 'Ses algılanamadı.');
            };

            rec.onend = function () {
                self.isListening = false;
                // Mikrofon kapandığında son konuşulan metin varsa kaybolmasın, Blazor'a kesin teslim et
                if (self.lastTranscript && self.lastTranscript.trim()) {
                    dotNetHelper.invokeMethodAsync('OnSpeechResult', self.lastTranscript.trim(), true);
                    self.lastTranscript = '';
                }
                dotNetHelper.invokeMethodAsync('OnSpeechEnd');
            };

            this.recognition = rec;
            rec.start();
            return true;
        } catch (e) {
            this.isListening = false;
            dotNetHelper.invokeMethodAsync('OnSpeechError', e.message);
            return false;
        }
    },

    stopListening: function () {
        if (this.recognition) {
            try {
                this.recognition.stop();
            } catch (e) { }
        }
        this.isListening = false;
    },

    // Doğal, net ve akıcı Türkçe asistan sesini seçer
    getFemaleVoice: function (lang) {
        if (!window.speechSynthesis) return null;
        var voices = this.cachedVoices && this.cachedVoices.length > 0
            ? this.cachedVoices
            : window.speechSynthesis.getVoices();

        if (!voices || voices.length === 0) return null;

        // 1. Türkçe asistan sesleri
        // Microsoft Emel Online (Natural), Microsoft Dilara, Google Türkçe, Filiz, Banu vb.
        var trVoices = voices.filter(function (v) {
            return v.lang && v.lang.toLowerCase().startsWith('tr');
        });

        if (trVoices.length > 0) {
            // Kadın ismi içeren Türkçe sesler
            var femaleTr = trVoices.find(function (v) {
                var name = (v.name || '').toLowerCase();
                return name.includes('emel') || name.includes('dilara') || name.includes('filiz') ||
                       name.includes('seda') || name.includes('banu') || name.includes('zeynep') ||
                       name.includes('ayşe') || name.includes('female') || name.includes('kadın') ||
                       name.includes('google') || name.includes('natural');
            });
            if (femaleTr) return femaleTr;

            // Tolga haricindeki ses
            var notTolga = trVoices.find(function (v) {
                return !((v.name || '').toLowerCase().includes('tolga'));
            });
            if (notTolga) return notTolga;

            return trVoices[0];
        }

        // 2. Global kadın sesleri
        var anyFemale = voices.find(function (v) {
            var name = (v.name || '').toLowerCase();
            return name.includes('female') || name.includes('zira') || name.includes('samantha') ||
                   name.includes('victoria') || name.includes('karen') || name.includes('hazel');
        });

        return anyFemale || voices[0];
    },

    speakText: function (text, lang) {
        if (!window.speechSynthesis) return;

        try {
            window.speechSynthesis.cancel(); // Önceki okumayı durdur

            // Markdown sembolleri, linkler ve emojileri temizle
            var cleanText = text
                .replace(/[#*`_~]/g, '')
                .replace(/https?:\/\/\S+/g, '')
                .replace(/•/g, '')
                .replace(/[\u{1F600}-\u{1F64F}\u{1F300}-\u{1F5FF}\u{1F680}-\u{1F6FF}\u{2600}-\u{26FF}\u{2700}-\u{27BF}]/gu, '')
                .trim();

            if (!cleanText) return;

            var self = this;
            var executeSpeak = function () {
                var utterance = new SpeechSynthesisUtterance(cleanText);
                utterance.lang = lang || 'tr-TR';
                
                // Doğal konuşma perdesi (pitch: 1.0) ve akıcı hız (rate: 1.0)
                utterance.pitch = 1.0;
                utterance.rate = 1.0;
                utterance.volume = 1.0;

                var femaleVoice = self.getFemaleVoice(lang || 'tr-TR');
                if (femaleVoice) {
                    utterance.voice = femaleVoice;
                }

                window.speechSynthesis.speak(utterance);
            };

            var currentVoices = window.speechSynthesis.getVoices();
            if (!currentVoices || currentVoices.length === 0) {
                window.speechSynthesis.onvoiceschanged = function () {
                    self.cachedVoices = window.speechSynthesis.getVoices() || [];
                    executeSpeak();
                };
            } else {
                self.cachedVoices = currentVoices;
                executeSpeak();
            }
        } catch (e) {
            console.warn('SpeechSynthesis error:', e);
        }
    },

    stopSpeaking: function () {
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }
    },

    // Sohbet geçmişini LocalStorage'da saklama ve yükleme
    saveHistory: function (jsonStr) {
        try {
            localStorage.setItem('otelpro_copilot_chat', jsonStr);
        } catch (e) { }
    },

    loadHistory: function () {
        try {
            return localStorage.getItem('otelpro_copilot_chat') || '';
        } catch (e) {
            return '';
        }
    },

    clearHistory: function () {
        try {
            localStorage.removeItem('otelpro_copilot_chat');
        } catch (e) { }
    }
};

// Sayfa yüklendiğinde sesleri başlat
if (typeof window !== 'undefined') {
    window.OtelVoice.initVoices();
}
