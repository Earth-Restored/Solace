import { SkinViewer, IdleAnimation } from 'skinview3d';

export class SkinPreviewWrapper {
    constructor(containerElement, skinBase64, isSlim) {
        this.container = containerElement;
        this.container.innerHTML = '';

        this.spinner = document.createElement('div');
        this.spinner.className = 'spinner-border text-primary';
        this.spinner.setAttribute('role', 'status');
        this.spinner.innerHTML = '<span class="visually-hidden">Loading skin...</span>';
        this.container.appendChild(this.spinner);

        this.canvas = document.createElement('canvas');
        this.canvas.style.display = 'none';
        this.canvas.style.width = '100%';
        this.canvas.style.height = '100%';
        this.container.appendChild(this.canvas);

        const width = this.container.clientWidth || 200;
        const height = this.container.clientHeight || 250;

        this.viewer = new SkinViewer({
            canvas: this.canvas,
            width: width,
            height: height,
            model: isSlim ? 'slim' : 'default'
        });

        this.viewer.animation = new IdleAnimation();

        if (this.viewer.controls) {
            this.viewer.controls.enableZoom = true;
            this.viewer.controls.enableRotate = true;
            this.viewer.controls.enablePan = false;
        }

        if (skinBase64) {
            this.loadSkin(skinBase64, isSlim);
        }
    }

    async loadSkin(skinBase64, isSlim) {
        try {
            if (this.spinner) {
                this.spinner.style.display = 'block';
            }
            this.canvas.style.display = 'none';

            const dataUrl = skinBase64.startsWith('data:')
                ? skinBase64
                : `data:image/png;base64,${skinBase64}`;

            await this.viewer.loadSkin(dataUrl, {
                model: isSlim ? 'slim' : 'default'
            });

            if (this.spinner) {
                this.spinner.style.display = 'none';
            }
            this.canvas.style.display = 'block';
        } catch (err) {
            console.error('Failed to render 3D skin:', err);
            if (this.spinner) {
                this.spinner.style.display = 'none';
            }
        }
    }

    dispose() {
        if (this.viewer) {
            this.viewer.dispose();
            this.viewer = null;
        }
        if (this.container) {
            this.container.innerHTML = '';
        }
    }
}

export function createSkinPreview(containerElement, skinBase64, isSlim) {
    return new SkinPreviewWrapper(containerElement, skinBase64, isSlim);
}
