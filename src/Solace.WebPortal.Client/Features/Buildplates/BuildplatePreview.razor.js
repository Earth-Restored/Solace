import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { PointerLockControls } from 'three/addons/controls/PointerLockControls.js';

export function initThreeJs(container, dataUri, size, isNight, dotNetRef) {
    if (!container) {
        return;
    }

    dispose(container);

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

    const camera = new THREE.PerspectiveCamera(75, container.clientWidth / container.clientHeight, 0.1, 10000);
    const cameraDistance = size * 1.25;
    camera.position.set(-cameraDistance, cameraDistance, -cameraDistance);
    camera.lookAt(0, 0, 0);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.setPixelRatio(window.devicePixelRatio);
    container.appendChild(renderer.domElement);

    const orbitControls = new OrbitControls(camera, renderer.domElement);
    orbitControls.target.set(0, 0, 0);
    orbitControls.update();

    const flyControls = new PointerLockControls(camera, renderer.domElement);

    const moveState = {
        forward: false,
        backward: false,
        left: false,
        right: false,
        up: false,
        down: false
    };

    const onKeyDown = (event) => {
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

        if (!flyControls.isLocked) {
            return;
        }

        switch (event.code) {
            case 'KeyW': moveState.forward = true; break;
            case 'KeyS': moveState.backward = true; break;
            case 'KeyA': moveState.left = true; break;
            case 'KeyD': moveState.right = true; break;
            case 'Space': moveState.up = true; break;
            case 'ShiftLeft':
            case 'ShiftRight': moveState.down = true; break;
        }
    };

    const onKeyUp = (event) => {
        if (!state.isFirstPerson) {
            return;
        }

        switch (event.code) {
            case 'KeyW': moveState.forward = false; break;
            case 'KeyS': moveState.backward = false; break;
            case 'KeyA': moveState.left = false; break;
            case 'KeyD': moveState.right = false; break;
            case 'Space': moveState.up = false; break;
            case 'ShiftLeft':
            case 'ShiftRight': moveState.down = false; break;
        }
    };

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);

    const onUnlock = () => {
        if (state.isFirstPerson && state.dotNetRef) {
            state.dotNetRef.invokeMethodAsync('ExitFirstPersonMode');
        }
    };
    flyControls.addEventListener('unlock', onUnlock);

    const loader = new GLTFLoader();
    loader.load(dataUri, (gltf) => {
        scene.add(gltf.scene);
    }, undefined, (error) => {
        console.error('Error loading 3D GLTF model:', error);
    });

    const clock = new THREE.Clock();
    const state = {
        renderer,
        animationId: null,
        resizeObserver: null,
        orbitControls,
        flyControls,
        isFirstPerson: false,
        onKeyDown,
        onKeyUp,
        onUnlock,
        dotNetRef
    };

    const flySpeed = Math.max(size, 10);

    const animate = function () {
        state.animationId = requestAnimationFrame(animate);
        const delta = clock.getDelta();

        if (state.isFirstPerson) {
            if (flyControls.isLocked) {
                const actualSpeed = flySpeed * delta;
                if (moveState.forward) flyControls.moveForward(actualSpeed);
                if (moveState.backward) flyControls.moveForward(-actualSpeed);
                if (moveState.left) flyControls.moveRight(-actualSpeed);
                if (moveState.right) flyControls.moveRight(actualSpeed);
                if (moveState.up) camera.position.y += actualSpeed;
                if (moveState.down) camera.position.y -= actualSpeed;
            }
        } else {
            orbitControls.update();
        }

        renderer.render(scene, camera);
    };
    animate();

    state.resizeObserver = new ResizeObserver(() => {
        if (container.clientWidth && container.clientHeight) {
            camera.aspect = container.clientWidth / container.clientHeight;
            camera.updateProjectionMatrix();
            renderer.setSize(container.clientWidth, container.clientHeight);
        }
    });
    state.resizeObserver.observe(container);

    container.__threeJsState = state;
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
        state.flyControls.lock();
    } else {
        state.flyControls.unlock();
        state.orbitControls.enabled = true;

        const dir = new THREE.Vector3();
        state.orbitControls.object.getWorldDirection(dir);
        state.orbitControls.target.copy(state.orbitControls.object.position).add(dir.multiplyScalar(10));
        state.orbitControls.update();
    }
}

export function dispose(container) {
    if (!container || !container.__threeJsState) {
        return;
    }

    const state = container.__threeJsState;

    if (state.animationId) cancelAnimationFrame(state.animationId);
    if (state.resizeObserver) state.resizeObserver.disconnect();
    if (state.onKeyDown) window.removeEventListener('keydown', state.onKeyDown);
    if (state.onKeyUp) window.removeEventListener('keyup', state.onKeyUp);

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

    if (state.renderer) {
        state.renderer.dispose();
        if (container.contains(state.renderer.domElement)) {
            container.removeChild(state.renderer.domElement);
        }
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