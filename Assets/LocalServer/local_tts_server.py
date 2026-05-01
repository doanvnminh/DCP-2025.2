from flask import Flask, request
import os
import re
import sys
from piper.voice import PiperVoice, SynthesisConfig

app = Flask(__name__)

# =========================================================
# PIPER TTS SETUP - ABSOLUTE PATHING
# =========================================================
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

#MODEL_NAME = "en_US-libritts_r-medium"
MODEL_NAME = "en_US-libritts-high"
MODEL_FILE = os.path.join(SCRIPT_DIR, f"{MODEL_NAME}.onnx")
CONFIG_FILE = os.path.join(SCRIPT_DIR, f"{MODEL_NAME}.onnx.json")

print("\n=========================================")
print("1. Checking Piper AI Brain...")
print(f"Looking in: {SCRIPT_DIR}")

if not os.path.exists(MODEL_FILE):
    print(f"🚨 [ERROR] Cannot find the brain file at: {MODEL_FILE}")
    sys.exit(1)

onnx_size_bytes = os.path.getsize(MODEL_FILE)
onnx_size_mb = onnx_size_bytes / (1024 * 1024)

if onnx_size_mb < 50: 
    print(f"🚨 [CRITICAL ERROR] The file is only {onnx_size_mb:.2f} MB!")
    print(f"You have a corrupted 'Ghost File'. Delete it and download the real 63MB one.")
    sys.exit(1)

print(f"✅ AI Brain is valid! ({onnx_size_mb:.2f} MB). Loading into CPU Memory...")
voice = PiperVoice.load(MODEL_FILE, CONFIG_FILE)

# =========================================================
# THE API SERVER (PURE PCM STREAM)
# =========================================================
@app.route('/generate', methods=['POST'])
def generate_audio():
    data = request.json
    raw_text = data.get("text", "Testing Piper voice generation.")
    
    clean_text = re.sub(r'\s+', ' ', raw_text).strip()
    
    print(f"\n[RECEIVE] Unity asked: {clean_text}")
    print("[PROCESS] Piper is synthesizing pure raw audio data...")
    
    raw_pcm_bytes = b""
    try:
        #reduce the speech speed
       # custom_config = SynthesisConfig(length_scale=1.5) 
        # THE FINAL FIX: 
        # We must use a 'for' loop to force the lazy generator to wake up and work!
        for audio_chunk in voice.synthesize(clean_text):
            # Extract the raw 16-bit sound data from the chunk
            raw_pcm_bytes += audio_chunk.audio_int16_bytes
            
    except Exception as e:
        print(f"🚨 [PIPER CRASHED DURING SYNTHESIS] {e}")
        return "Internal AI Error", 500

    print(f"[DEBUG] Total pure sound generated: {len(raw_pcm_bytes)} bytes")
    
    if len(raw_pcm_bytes) == 0:
        print("🚨 [ERROR] Piper successfully ran, but generated 0 bytes of audio.")
        return "Empty Audio Error", 500
    
    print(f"[SEND] Streaming {len(raw_pcm_bytes)} bytes of raw sound to Unity...")
    
    return raw_pcm_bytes, 200, {'Content-Type': 'application/octet-stream'}

if __name__ == '__main__':
    print(f"\n=========================================")
    print(f"PIPER RAW PCM SERVER RUNNING ON PORT 5000")
    print(f"=========================================\n")
    app.run(host='127.0.0.1', port=5000)