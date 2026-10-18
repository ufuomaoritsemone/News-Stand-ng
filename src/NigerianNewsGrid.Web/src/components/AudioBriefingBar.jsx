import React, { useState, useEffect, useRef } from 'react';
import { Volume2, Play, Pause, X, Radio } from 'lucide-react';
import { api } from '../services/api';
import { trackAudioBriefing } from '../services/analytics';

export default function AudioBriefingBar({ onClose, compact = false }) {
  const [briefing, setBriefing] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [loading, setLoading] = useState(true);
  const audioRef = useRef(null);

  useEffect(() => {
    let isMounted = true;
    api.getLatestAudioBriefing()
      .then((data) => {
        if (isMounted && data && data.available && data.streamUrl) {
          setBriefing(data);
        }
      })
      .catch(() => {})
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const togglePlay = () => {
    if (!audioRef.current) return;
    if (isPlaying) {
      audioRef.current.pause();
      setIsPlaying(false);
    } else {
      audioRef.current.play()
        .then(() => setIsPlaying(true))
        .catch((e) => console.warn('Audio playback error:', e.message));
    }
  };

  const handleTimeUpdate = () => {
    if (audioRef.current) {
      setCurrentTime(audioRef.current.currentTime);
      setDuration(audioRef.current.duration || 0);
    }
  };

  const handleSeek = (e) => {
    const time = Number(e.target.value);
    if (audioRef.current) {
      audioRef.current.currentTime = time;
      setCurrentTime(time);
    }
  };

  const formatSecs = (s) => {
    if (!s || isNaN(s)) return '0:00';
    const m = Math.floor(s / 60);
    const secs = Math.floor(s % 60);
    return `${m}:${secs < 10 ? '0' : ''}${secs}`;
  };

  if (loading || !briefing) return null;

  const playerContent = (
    <div className={`audio-briefing-inner ${compact ? 'audio-briefing-inner--compact' : ''}`}>
      <audio
        ref={audioRef}
        src={briefing.streamUrl}
        onTimeUpdate={handleTimeUpdate}
        onEnded={() => {
          setIsPlaying(false);
          trackAudioBriefing('completed', briefing.timeOfDay || 'Daily Briefing');
        }}
        onPause={() => {
          setIsPlaying(false);
          trackAudioBriefing('pause', briefing.timeOfDay || 'Daily Briefing');
        }}
        onPlay={() => {
          setIsPlaying(true);
          trackAudioBriefing('play', briefing.timeOfDay || 'Daily Briefing');
        }}
        preload="metadata"
      />

      <div className="audio-briefing-left">
        <div className="audio-badge">
          <Radio size={13} className="pulse-icon" />
          <span>{compact ? 'AUDIO BRIEFING' : 'DAILY AUDIO BRIEFING'}</span>
        </div>
        <div className="audio-briefing-title">
          <span>{briefing.timeOfDay ? `${briefing.timeOfDay.toUpperCase()} EDITION` : 'TODAY’S HEADLINES'}</span>
          {!compact && <span className="audio-voice-tag">🎙️ Nigerian Neural Voice</span>}
        </div>
      </div>

      <div className="audio-briefing-center">
        <button
          type="button"
          className="audio-play-btn"
          onClick={togglePlay}
          aria-label={isPlaying ? 'Pause briefing' : 'Play audio briefing'}
          title={isPlaying ? 'Pause' : 'Play'}
        >
          {isPlaying ? <Pause size={15} /> : <Play size={15} style={{ marginLeft: 2 }} />}
        </button>

        <span className="audio-time-label">{formatSecs(currentTime)}</span>

        <input
          type="range"
          min={0}
          max={duration || 100}
          value={currentTime}
          onChange={handleSeek}
          className="audio-seek-slider"
          aria-label="Audio progress"
        />

        <span className="audio-time-label">{formatSecs(duration)}</span>
      </div>

      {onClose && (
        <button
          type="button"
          className="audio-close-btn"
          onClick={onClose}
          aria-label="Dismiss audio player"
        >
          <X size={16} />
        </button>
      )}
    </div>
  );

  if (compact) {
    return (
      <div className="audio-briefing-inline" role="region" aria-label="Audio Daily Briefing">
        {playerContent}
      </div>
    );
  }

  return (
    <div className="audio-briefing-bar app-container" role="region" aria-label="Audio Daily Briefing">
      {playerContent}
    </div>
  );
}
