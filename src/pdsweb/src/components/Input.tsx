import type { InputHTMLAttributes, Ref, TextareaHTMLAttributes } from 'react';

const base =
  'w-full rounded-sm bg-surface border border-input text-ink placeholder:text-muted ' +
  'focus:border-focus-ring focus:outline-none focus:ring-2 focus:ring-focus-ring/30 ' +
  'disabled:opacity-50 disabled:cursor-not-allowed';

const pad = 'px-3 py-2 text-sm';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  ref?: Ref<HTMLInputElement>;
}

interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  ref?: Ref<HTMLTextAreaElement>;
}

export function Input({ ref, className = '', ...rest }: InputProps) {
  return <input ref={ref} className={`${base} ${pad} ${className}`} {...rest} />;
}

export function Textarea({ ref, className = '', ...rest }: TextareaProps) {
  return <textarea ref={ref} className={`${base} ${pad} ${className}`} {...rest} />;
}