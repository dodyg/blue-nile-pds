import { useEffect, useRef } from 'react';

const SCRIPT_URL = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

let scriptPromise: Promise<void> | null = null;

function loadTurnstileScript(): Promise<void> {
  if (typeof window.turnstile !== 'undefined') {
    return Promise.resolve();
  }
  if (scriptPromise === null) {
    scriptPromise = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = SCRIPT_URL;
      script.async = true;
      script.defer = true;
      script.onload = () => resolve();
      script.onerror = () => {
        scriptPromise = null;
        reject(new Error('Failed to load Turnstile script'));
      };
      document.head.appendChild(script);
    });
  }
  return scriptPromise;
}

interface TurnstileWidgetProps {
  siteKey: string;
  onVerify: (token: string) => void;
  onExpire?: () => void;
  onError?: () => void;
  onUnsupported?: () => void;
  resetKey?: number;
}

export default function TurnstileWidget({
  siteKey,
  onVerify,
  onExpire,
  onError,
  onUnsupported,
  resetKey = 0,
}: TurnstileWidgetProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const widgetIdRef = useRef<string | null>(null);
  const callbacksRef = useRef({ onVerify, onExpire, onError, onUnsupported });
  callbacksRef.current = { onVerify, onExpire, onError, onUnsupported };

  useEffect(() => {
    let cancelled = false;

    loadTurnstileScript().then(
      () => {
        if (cancelled || !containerRef.current || typeof window.turnstile === 'undefined') {
          return;
        }
        widgetIdRef.current = window.turnstile.render(containerRef.current, {
          sitekey: siteKey,
          callback: (token: string) => callbacksRef.current.onVerify(token),
          'expired-callback': () => callbacksRef.current.onExpire?.(),
          'error-callback': () => callbacksRef.current.onError?.(),
          'unsupported-callback': () => callbacksRef.current.onUnsupported?.(),
        });
      },
      () => callbacksRef.current.onError?.(),
    );

    return () => {
      cancelled = true;
      if (widgetIdRef.current !== null && typeof window.turnstile !== 'undefined') {
        window.turnstile.remove(widgetIdRef.current);
        widgetIdRef.current = null;
      }
    };
  }, [siteKey]);

  useEffect(() => {
    if (resetKey > 0 && widgetIdRef.current !== null && typeof window.turnstile !== 'undefined') {
      window.turnstile.reset(widgetIdRef.current);
    }
  }, [resetKey]);

  return <div ref={containerRef} />;
}
