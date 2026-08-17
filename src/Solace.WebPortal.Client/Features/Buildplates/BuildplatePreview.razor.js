import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { PointerLockControls } from 'three/addons/controls/PointerLockControls.js';

class Timer {
    constructor() {
        this._previousTime = performance.now();
        this._delta = 0;
    }

    update() {
        const currentTime = performance.now();
        this._delta = (currentTime - this._previousTime) / 1000;
        this._previousTime = currentTime;
    }

    getDelta() {
        return this._delta;
    }

    reset() {
        this._previousTime = performance.now();
        this._delta = 0;
    }
}

export function setupLazyLoad(container, dotNetRef) {
    if (!container) {
        return;
    }

    const observer = new IntersectionObserver((entries) => {
        if (entries[0].isIntersecting) {
            dotNetRef.invokeMethodAsync('StartLoadingAsync');

            observer.disconnect();
            delete container.__lazyObserver;
        }
    }, {
        rootMargin: '400px'
    });

    observer.observe(container);
    container.__lazyObserver = observer;
}

export function cleanupLazyLoad(container) {
    if (container && container.__lazyObserver) {
        container.__lazyObserver.disconnect();
        delete container.__lazyObserver;
    }
}

let sharedRenderer = null;
const activeViews = new Set();
let globalAnimationId = null;
let globalObserver = null;

function getSharedRenderer() {
    if (!sharedRenderer) {
        sharedRenderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
        sharedRenderer.setPixelRatio(1);
    }

    return sharedRenderer;
}

function getObserver() {
    if (!globalObserver) {
        globalObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                const state = entry.target.__threeJsState;
                if (state) {
                    state.isVisible = entry.isIntersecting;

                    if (entry.isIntersecting) {
                        state.timer.reset();
                    }
                }
            });
        }, { threshold: 0 });
    }

    return globalObserver;
}

function animateGlobal() {
    globalAnimationId = requestAnimationFrame(animateGlobal);

    const renderer = getSharedRenderer();
    const pixelRatio = window.devicePixelRatio || 1;

    for (const state of activeViews) {
        if (!state.isVisible) {
            continue;
        }

        state.timer.update();
        const delta = state.timer.getDelta();

        if (state.isFirstPerson) {
            if (state.flyControls.isLocked) {
                const actualSpeed = state.flySpeed * delta;
                if (state.moveState.forward) state.flyControls.moveForward(actualSpeed);
                if (state.moveState.backward) state.flyControls.moveForward(-actualSpeed);
                if (state.moveState.left) state.flyControls.moveRight(-actualSpeed);
                if (state.moveState.right) state.flyControls.moveRight(actualSpeed);
                if (state.moveState.up) state.camera.position.y += actualSpeed;
                if (state.moveState.down) state.camera.position.y -= actualSpeed;
            }
        } else {
            if (state.checkHasCenterMoved() || state.isPointerDown) {
                state.orbitControls.autoRotate = false;
            }

            state.orbitControls.update();
        }

        const container = state.container;
        const width = container.clientWidth;
        const height = container.clientHeight;

        if (width === 0 || height === 0) {
            continue;
        }

        const renderWidth = Math.floor(width * pixelRatio);
        const renderHeight = Math.floor(height * pixelRatio);

        if (state.canvas2d.width !== renderWidth || state.canvas2d.height !== renderHeight) {
            state.canvas2d.width = renderWidth;
            state.canvas2d.height = renderHeight;
            state.camera.aspect = width / height;
            state.camera.updateProjectionMatrix();
        }

        renderer.setSize(renderWidth, renderHeight, false);
        renderer.render(state.scene, state.camera);
        state.ctx2d.drawImage(renderer.domElement, 0, 0);
    }
}

export function initThreeJs(container, dataUri, bounds, isNight, dotNetRef) {
    if (!container) {
        return;
    }

    dispose(container);

    const minX = bounds.minX ?? bounds.MinX ?? 0;
    const minY = bounds.minY ?? bounds.MinY ?? 0;
    const minZ = bounds.minZ ?? bounds.MinZ ?? 0;
    const maxX = bounds.maxX ?? bounds.MaxX ?? 0;
    const maxY = bounds.maxY ?? bounds.MaxY ?? 0;
    const maxZ = bounds.maxZ ?? bounds.MaxZ ?? 0;

    const center = new THREE.Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
    const initialCenter = center.clone();

    const sizeVector = new THREE.Vector3(maxX - minX, maxY - minY, maxZ - minZ);
    const size = sizeVector.length();

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(isNight ? "#070066" : "#3B69F5");

    const ambientLight = new THREE.AmbientLight(0xffffff, 1.5);
    scene.add(ambientLight);

    const mainLight = new THREE.DirectionalLight(0xffffff, 2.5);
    mainLight.position.set(50, 100, 50);
    scene.add(mainLight);

    const fillLight = new THREE.DirectionalLight(0xffffff, 1.0);
    fillLight.position.set(-50, 50, -50);
    scene.add(fillLight);

    const aspect = container.clientWidth / container.clientHeight || 1;
    const camera = new THREE.PerspectiveCamera(75, aspect, 0.1, 10000);

    const viewMargin = 1.05;
    const radius = (size / 2) * viewMargin;
    const fovRad = (camera.fov * Math.PI) / 180;
    const effectiveFov = 2 * Math.atan(Math.tan(fovRad / 2) * Math.min(1, aspect));
    const distance = radius / Math.sin(effectiveFov / 2);

    const angleRad = (30 * Math.PI) / 180;
    const cosAngle = Math.cos(angleRad);
    const sinAngle = Math.sin(angleRad);

    const dir = new THREE.Vector3(Math.SQRT1_2 * cosAngle, sinAngle, Math.SQRT1_2 * cosAngle);
    camera.position.copy(center).addScaledVector(dir, distance);
    camera.lookAt(center);

    const canvas2d = document.createElement('canvas');
    canvas2d.style.width = '100%';
    canvas2d.style.height = '100%';
    canvas2d.style.display = 'block';
    container.appendChild(canvas2d);

    const ctx2d = canvas2d.getContext('2d', { alpha: false });

    const orbitControls = new OrbitControls(camera, canvas2d);
    orbitControls.target.copy(center);
    orbitControls.autoRotate = true;
    orbitControls.autoRotateSpeed = 2.0;
    orbitControls.update();

    const flyControls = new PointerLockControls(camera, canvas2d);

    const moveState = { forward: false, backward: false, left: false, right: false, up: false, down: false };

    const state = {
        container,
        canvas2d,
        ctx2d,
        scene,
        camera,
        orbitControls,
        flyControls,
        moveState,
        timer: new Timer(),
        isVisible: false,
        isFirstPerson: false,
        isPointerDown: false,
        autoRotateTimeout: null,
        flySpeed: 10,
        dotNetRef,

        checkHasCenterMoved: () => orbitControls.target.distanceToSquared(initialCenter) > 0.0001,
        checkCanAutoRotate: () => !state.checkHasCenterMoved() && !state.isPointerDown && !state.isFirstPerson
    };

    state.onPointerDown = () => {
        state.isPointerDown = true;
        state.orbitControls.autoRotate = false;

        if (state.autoRotateTimeout) {
            clearTimeout(state.autoRotateTimeout);
        }
    };

    state.onPointerUp = () => {
        if (!state.isPointerDown) {
            return;
        }

        state.isPointerDown = false;

        if (state.autoRotateTimeout) {
            clearTimeout(state.autoRotateTimeout);
        }

        state.autoRotateTimeout = setTimeout(() => {
            if (state.checkCanAutoRotate()) {
                state.orbitControls.autoRotate = true;
            }
        }, 1000);
    };

    state.onKeyDown = (event) => {
        if (!state.isFirstPerson) {
            return;
        }

        if (event.code === 'Space') {
            event.preventDefault();
        }

        if (event.code === 'Tab') {
            if (state.dotNetRef) {
                state.dotNetRef.invokeMethodAsync('ExitFirstPersonMode');
            }

            return;
        }

        if (!state.flyControls.isLocked) {
            return;
        }

        switch (event.code) {
            case 'KeyW': state.moveState.forward = true; break;
            case 'KeyS': state.moveState.backward = true; break;
            case 'KeyA': state.moveState.left = true; break;
            case 'KeyD': state.moveState.right = true; break;
            case 'Space': state.moveState.up = true; break;
            case 'ShiftLeft':
            case 'ShiftRight': state.moveState.down = true; break;
        }
    };

    state.onKeyUp = (event) => {
        if (!state.isFirstPerson) {
            return;
        }

        switch (event.code) {
            case 'KeyW': state.moveState.forward = false; break;
            case 'KeyS': state.moveState.backward = false; break;
            case 'KeyA': state.moveState.left = false; break;
            case 'KeyD': state.moveState.right = false; break;
            case 'Space': state.moveState.up = false; break;
            case 'ShiftLeft':
            case 'ShiftRight': state.moveState.down = false; break;
        }
    };

    state.onUnlock = () => {
        if (state.isFirstPerson && state.dotNetRef) {
            state.dotNetRef.invokeMethodAsync('ExitFirstPersonMode');
        }
    };

    canvas2d.addEventListener('pointerdown', state.onPointerDown);
    window.addEventListener('pointerup', state.onPointerUp);
    window.addEventListener('pointercancel', state.onPointerUp);
    window.addEventListener('keydown', state.onKeyDown);
    window.addEventListener('keyup', state.onKeyUp);
    flyControls.addEventListener('unlock', state.onUnlock);

    const loader = new GLTFLoader();
    loader.load(dataUri, (gltf) => {
        scene.add(gltf.scene);
    }, undefined, (error) => {
        console.error('Error loading 3D GLTF model:', error);
    });

    container.__threeJsState = state;
    activeViews.add(state);
    getObserver().observe(container);

    if (!globalAnimationId) {
        animateGlobal();
    }
}

export function setControlMode(container, isFirstPerson) {
    if (!container || !container.__threeJsState) {
        return;
    }

    const state = container.__threeJsState;

    if (document.activeElement && document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }

    state.isFirstPerson = isFirstPerson;

    if (isFirstPerson) {
        state.orbitControls.enabled = false;
        state.orbitControls.autoRotate = false;
        if (state.autoRotateTimeout) {
            clearTimeout(state.autoRotateTimeout);
        }

        state.flyControls.lock();
    } else {
        state.flyControls.unlock();
        state.orbitControls.enabled = true;

        const dir = new THREE.Vector3();
        state.orbitControls.object.getWorldDirection(dir);
        state.orbitControls.target.copy(state.orbitControls.object.position).add(dir.multiplyScalar(10));
        state.orbitControls.update();

        state.orbitControls.autoRotate = !!state.checkCanAutoRotate();
    }
}

export function dispose(container) {
    if (!container || !container.__threeJsState) {
        return;
    }

    const state = container.__threeJsState;

    if (globalObserver) {
        globalObserver.unobserve(container);
    }

    activeViews.delete(state);

    if (state.autoRotateTimeout) {
        clearTimeout(state.autoRotateTimeout);
    }

    if (state.onKeyDown) {
        window.removeEventListener('keydown', state.onKeyDown);
    }
    if (state.onKeyUp) {
        window.removeEventListener('keyup', state.onKeyUp);
    }
    if (state.onPointerUp) {
        window.removeEventListener('pointerup', state.onPointerUp);
        window.removeEventListener('pointercancel', state.onPointerUp);
    }

    if (state.canvas2d && state.onPointerDown) {
        state.canvas2d.removeEventListener('pointerdown', state.onPointerDown);
    }

    if (state.flyControls) {
        if (state.onUnlock) {
            state.flyControls.removeEventListener('unlock', state.onUnlock);
        }

        state.flyControls.unlock();
        state.flyControls.dispose();
    }
    if (state.orbitControls) {
        state.orbitControls.dispose();
    }

    if (state.scene) {
        state.scene.traverse((child) => {
            if (child.isMesh) {
                if (child.geometry) {
                    child.geometry.dispose();
                }

                if (child.material) {
                    if (Array.isArray(child.material)) {
                        child.material.forEach(m => m.dispose());
                    }
                    else {
                        child.material.dispose();
                    }
                }
            }
        });
    }

    if (state.canvas2d && container.contains(state.canvas2d)) {
        container.removeChild(state.canvas2d);
    }

    delete container.__threeJsState;
}

export function toggleFullscreen(container) {
    if (!document.fullscreenElement) {
        container.requestFullscreen().catch(err => console.error(err.message));
    } else {
        document.exitFullscreen();
    }
}