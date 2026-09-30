import { CLOUDINARY_WIDGET_URL } from './constants.js';

let pending;

// Share a single in-flight request across registration routes; failed loads can retry.
export function loadCloudinaryWidget() {
  if (window.cloudinary) return Promise.resolve(window.cloudinary);
  if (pending) return pending;

  pending = new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = CLOUDINARY_WIDGET_URL;
    script.async = true;
    const fail = () => {
      clearTimeout(timeout);
      script.remove();
      pending = undefined;
      reject(new Error('Cloudinary widget could not be loaded'));
    };
    const timeout = setTimeout(fail, 15000);
    script.onerror = fail;
    script.onload = () => {
      clearTimeout(timeout);
      if (!window.cloudinary) return fail();
      resolve(window.cloudinary);
    };
    document.head.appendChild(script);
  });
  return pending;
}
