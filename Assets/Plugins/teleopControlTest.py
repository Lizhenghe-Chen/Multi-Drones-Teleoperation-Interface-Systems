from ctypes import *
import subprocess
import numpy as np
from numpy.lib import recfunctions as rfn
from numpy.ctypeslib import ndpointer
import time
import math
import random
import psutil
import threading
import copy
from scipy.linalg import block_diag, inv
from mmtimer import mmtimer
import matplotlib.pyplot as plt
import argparse
import os
#os.system("cls")

#-- Heli structure in shared memory used in the dll
class Heli(Structure):
    _fields_ = [('x', c_double),
                ('y', c_double),
                ('z', c_double),
                ('vx', c_double),
                ('vy', c_double),
                ('vz', c_double),
                ('u', c_double),
                ('v', c_double),
                ('w', c_double),
                ('p', c_double),
                ('q', c_double),
                ('r', c_double),
                ('roll', c_double),
                ('pitch', c_double),
                ('yaw', c_double),
                ('a1', c_double),
                ('b1', c_double),
                ('omega', c_double),
                ('pe', c_double),
                ('vxh', c_double),
                ('vyh', c_double),
                ('vzh', c_double),
                ('wzh', c_double),
                ('tiempoSim', c_double)]       

def run():
    parser = argparse.ArgumentParser(description="Teleoperation control test",
                                    formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    parser.add_argument("simulationTime", type=int, help="simulation time [s]")
    parser.add_argument("controlRate", type=int, help="control rate [Hz]")
    parser.add_argument("numberOfHelis", type=int, help="number of helicopters")
    parser.add_argument("ropeLength", type=float, help="rope length [m]")
    parser.add_argument("payloadMass", type=float, help="payload mass [kg]")
    parser.add_argument("payloadInitialX", type=float, help="payload initial x position [m]")
    parser.add_argument("payloadInitialY", type=float, help="payload initial y position [m]")
    parser.add_argument("payloadInitialZ", type=float, help="payload initial z position [m]")
    parser.add_argument("payloadInitialYaw", type=float, help="payload initial yaw [deg]")
    parser.add_argument("--aForce-altitude", default=20, dest="aForceAltitude", type=float, help="altitude to start using the artificial force [m]")
    parser.add_argument("--payload-size", default=0.25, dest="payloadSize", type=float, help="Payload (cube) edge length [m]")
    parser.add_argument("--spring-constant", default=35000, dest="springConstant", type=float, help="spring_constant [N/m]")
    parser.add_argument("--spring-friction", default=0.6, dest="springFriction", type=float, help="spring friction [N s/m]")
    parser.add_argument("--air-friction", default=0.001, dest="airFriction", type=float, help="air friction [N s/m]")
    parser.add_argument("--ground-repulsion", default=5, dest="groundRepulsion", type=float, help="ground repulsion [N/m]")
    parser.add_argument("--ground-friction", default=0.01, dest="groundFriction", type=float, help="ground friction [N s/m]")
    parser.add_argument("--ground-absorption", default=0.1, dest="groundAbsorption", type=float, help="ground absorption [N s/m]")
    args = parser.parse_args()
    config = vars(args)

    simulationTime = config['simulationTime']
    controlRate = config['controlRate']
    numberOfHelis = config['numberOfHelis']
    ropeLength = config['ropeLength']
    payloadMass = config['payloadMass']
    aForceAltitude = config['aForceAltitude']
    payloadSize = config['payloadSize']
    springConstant = config['springConstant']
    springFriction = config['springFriction']
    airFriction = config['airFriction']
    groundRepulsion = config['groundRepulsion']
    groundFriction = config['groundFriction']
    groundAbsorption = config['groundAbsorption']

    payloadInitialX = config['payloadInitialX']
    payloadInitialY = config['payloadInitialY']
    payloadInitialZ = config['payloadInitialZ']
    payloadInitialYaw = config['payloadInitialYaw']

    cv = np.zeros((4, 1), dtype=np.float32)
    cv[0,0] = payloadInitialX
    cv[1,0] = payloadInitialY
    cv[2,0] = payloadInitialZ
    cv[3,0] = payloadInitialYaw

    # Load DLL into memory.
    dllFileName = './SharedMemory.dll'
    sharedMemory = cdll.LoadLibrary(dllFileName)

    #-- Set dll functions and its arguments/result types
    startSimulation = sharedMemory.startSimulation
    startSimulation.argtypes = [c_int, c_float,  c_float, c_float, c_float, c_float, c_float, c_float, c_float, c_float, c_float, ndpointer(c_float)]

    stopSimulation = sharedMemory.stopSimulation
    stopSimulation.argtypes = [c_bool]

    setGlobalWind = sharedMemory.setGlobalWind
    setGlobalWind.argtypes = [c_float, c_float,  c_float]

    getLidarReadings = sharedMemory.getLidarReadings
    getLidarReadings.argtypes = [ndpointer(c_ushort),  c_int] # 2D float array

    getArtificialForce = sharedMemory.getArtificialForce
    getArtificialForce.argtypes = [ndpointer(c_float),  c_int]

    getRopeForce = sharedMemory.getRopeForce
    getRopeForce.argtypes = [ndpointer(c_float),  c_int]

    getRopePosition = sharedMemory.getRopePosition
    getRopePosition.argtypes = [ndpointer(c_float),  c_int] # 2D float array

    getHeliState = sharedMemory.getHeliState
    getHeliState.argtypes = [POINTER(Heli),  c_int]

    setHeliCommands = sharedMemory.setHeliCommands
    setHeliCommands.argtypes = [c_float, c_float,c_float, c_float, c_int]

    setVirtualLoad = sharedMemory.setVirtualLoad
    setVirtualLoad.argtypes = [ndpointer(c_float)]

    getUserCommands = sharedMemory.getUserCommands
    getUserCommands.argtypes = [ndpointer(c_float)]

    #-- Auxiliar matrices --------------------------
    aux1 = np.hstack((np.diag([1/numberOfHelis,1/numberOfHelis,1/numberOfHelis]),[[0], [0], [0]]))
    aux2 = np.hstack((np.eye(3), [[0], [0], [0]]));        # Original Matrix Delta
    aux3 = np.tile(aux2, (1,numberOfHelis))                # Repeat Matrix
    aux4 = np.hsplit(aux3,numberOfHelis)

    #-- Jacobians
    if numberOfHelis == 1:
        Jr = np.squeeze(aux4 - aux1)
    else:
        for i in range(numberOfHelis):
            if i == 0:
                aux5 = aux4[i]
            else:
                aux5 = block_diag(aux5,aux4[i])
        Jr = aux5 - np.tile(aux1, (numberOfHelis, numberOfHelis))
    Jl = np.tile(aux1,(1,numberOfHelis))
    Jp = np.zeros((numberOfHelis, numberOfHelis*4))
    for i in range(numberOfHelis):
        Jp[i, i*4+3] = 1;  

    #-- Jacobian matrix of the full system
    J = np.vstack((Jr[0:-3,:],Jl,Jp))

    filtro_n = 1
    filtro_fn = 0.1
    filtro_exir = 0.25 * np.eye(numberOfHelis*3)
    filtro_exil = 0.25
    filtro_epsi = 0.25
    filtro_xirdp = 0.2
    filtro_xcp = 0.01
    filtro_hp = 0.001
    #filtro_ffmean = 0.01
    #filtro_ffmeanp = 0.005
    ro = 1/numberOfHelis * np.ones((numberOfHelis, 1))
    Kpath = 2.0 * np.eye(3)
    Kpatht = 0.175 * np.eye(3)
    Krel = 2.0 * np.eye(numberOfHelis*3)
    Krelt = 0.15 * np.eye(numberOfHelis*3)
    Kpsi = 2.0 * np.eye(numberOfHelis)
    Kpsit = 0.1 * np.eye(numberOfHelis)
    Kffxyz = -50 * np.eye(3)
    Kffxyz[0,0] = -5
    Kffxyzt = 1.0*np.eye(3)
    Kffxyzt[0,0] = 0.1
    k_f = 0.0025
    numParticles = 100
    Rd = 5
    Ro = 1
    Zo = 0.25

    coords = np.array(range(2, numberOfHelis*3, 3))
    filtro_exir.ravel()[np.ravel_multi_index((coords, coords), filtro_exir.shape)] = 1.
    Krelt.ravel()[np.ravel_multi_index((coords, coords), Krelt.shape)] = 1.
    Krel.ravel()[np.ravel_multi_index((coords, coords), Krel.shape)] = 1.

    if(numberOfHelis==1):
        Kpatht = 0.04 * np.eye(3)
    elif(numberOfHelis==2):
        Kpatht = 0.055 * np.eye(3)
        k_f = 0.00175
    elif(numberOfHelis==3):
        Kpatht = 0.065 * np.eye(3)
        k_f = 0.0015
    elif(numberOfHelis==4):
        Kpatht = 0.075 * np.eye(3)
        k_f = 0.00125
    else:
        Kpatht = 0.085 * np.eye(3)
        k_f = 0.001

    angles = np.linspace(-math.pi / numberOfHelis, math.pi * (2-3/numberOfHelis), numberOfHelis)
    pt = Rd * np.array([np.cos(angles), np.sin(angles)])

    yXirdi = np.zeros((numberOfHelis*3, 1), dtype=np.float32)
    for i in range(numberOfHelis):
        yXirdi[i*3] = pt[0,i]
        yXirdi[i*3+1] = pt[1,i]
        yXirdi[i*3+2] = 0

    if(numberOfHelis==1):
        yXirdi[0] = 0
        yXirdi[1] = 0

    payloadInertia = payloadMass * 0.2**2
    cvp = np.zeros((4, 1), dtype=np.float32)
    cvpp = np.zeros((4, 1), dtype=np.float32)

    yXo = np.zeros((numberOfHelis*4, 1), dtype=np.float32)
    for i in range(numberOfHelis):
        yXo[i*4] = cv[0,0] + pt[0,i] + Ro * 2 * (random.random()-0.5)
        yXo[i*4+1] = cv[1,0] + pt[1,i] + Ro * 2 * (random.random()-0.5)
        yXo[i*4+2] = cv[2,0] + Zo
        yXo[i*4+3] = cv[3,0] + 360 * random.random()

    if(numberOfHelis==1):
        yXo[0] = cv[0,0] + Ro * 2 * (random.random()-0.5)
        yXo[1] = cv[1,0] + Ro * 2 * (random.random()-0.5)

    exeFile = './SimHelicop.exe'
    proc = subprocess.Popen(exeFile, creationflags=subprocess.CREATE_NEW_CONSOLE, close_fds=True)

    # list = [proc for proc in psutil.process_iter(['name']) if 'SimHelicop.exe' in str(proc.info['name'])]

    time.sleep(0.5)
            
    startSimulation(numberOfHelis, simulationTime * 2, payloadMass, payloadSize, ropeLength, springFriction, springConstant, airFriction, groundRepulsion, groundFriction, groundAbsorption, yXo)

    time.sleep(2.5 + 2.5 * numberOfHelis) # Wait some time so the simulation can properly start in the C++ program (the ropes/helis need time to move and stabilise)
    
    deltat = 1/controlRate
    tt = np.arange(0, simulationTime, deltat)
    size = len(tt)

    yHeli=np.zeros((numberOfHelis, 24, size), dtype=np.float32)
    yRope=np.zeros((numberOfHelis, numParticles, 3, size), dtype=np.float32)
    yForce=np.zeros((numberOfHelis, 3, size), dtype=np.float32, order='F')
    yFnorm=np.zeros((numberOfHelis, size), dtype=np.float32)
    yFForce=np.zeros((numberOfHelis, 3, size), dtype=np.float32)
    yXi=np.zeros((numberOfHelis, 3, size), dtype=np.float32)        #Vehicles Position
    yXip=np.zeros((numberOfHelis, 3, size), dtype=np.float32)       #Vehicles Speed
    yXir=np.zeros((numberOfHelis, 3, size), dtype=np.float32)
    yXird=np.tile(yXirdi, (1, size))
    yXirde=np.zeros((numberOfHelis*3, size), dtype=np.float32)
    yXirdp=np.zeros((numberOfHelis*3, size), dtype=np.float32)
    yXiprom=np.zeros((3, size), dtype=np.float32)
    yXil=np.zeros((3, size), dtype=np.float32)

    yXicNew=np.zeros((3, size), dtype=np.float32)
    yXicNewp=np.zeros((3, size), dtype=np.float32)

    yH=np.zeros((3, size), dtype=np.float32)
    yHe=np.zeros((3, size), dtype=np.float32)
    yHp=np.zeros((3, size), dtype=np.float32)
    yPdp=np.zeros(size, dtype=np.float32)
    yVd=np.zeros((3, size), dtype=np.float32)

    yXm=np.zeros((4, size), dtype=np.float32, order='F')

    yWind=np.zeros((3, size), dtype=np.float32)

    yFFmean=np.zeros((3, size), dtype=np.float32)
    yFFmeanp=np.zeros((3, size), dtype=np.float32)
    yFFmeanf=np.zeros((3, size), dtype=np.float32)
    yFFmeanpf=np.zeros((3, size), dtype=np.float32)

    yXYZd=np.zeros((3, size), dtype=np.float32)
    yXYZdp=np.zeros((3, size), dtype=np.float32)
    yPsid=np.zeros((1, size), dtype=np.float32)
    yPsidp=np.zeros((1, size), dtype=np.float32)

    yExil=np.zeros((3, size), dtype=np.float32)
    yExir=np.zeros((numberOfHelis*3, size), dtype=np.float32)
    yEpsi=np.zeros((numberOfHelis, size), dtype=np.float32)

    yPsi = np.zeros((numberOfHelis, size), dtype=np.float32)      #Vehicles orientation (Yaw)
    yPsip= np.zeros((numberOfHelis, size), dtype=np.float32)      #Vehicles angular speed (Yaw_p)
    yQ = np.zeros((numberOfHelis*4, size), dtype=np.float32)      #Vehicles state variables (Pos + Yaw)
    yQp = np.zeros((numberOfHelis*4, size), dtype=np.float32)     #Derivative of vehicles state variables

    yU=np.zeros((numberOfHelis*4, size), dtype=np.float32)
    yUc=np.zeros((numberOfHelis,4, size), dtype=np.float32)

    yPath_vc=np.zeros((3, size), dtype=np.float32)
    yRel_vc=np.zeros((numberOfHelis*3, size), dtype=np.float32)
    yPsi_vc=np.zeros((numberOfHelis, size), dtype=np.float32)

    yTsim=np.zeros(size, dtype=np.float32)
    yTsimHeli=np.zeros((numberOfHelis, size), dtype=np.float32)

    FFHelis = np.zeros((numberOfHelis, 3), dtype=np.float32)
    Kfiltro = np.zeros((numberOfHelis, filtro_n), dtype=np.float32)
    fn_ant = np.zeros((numberOfHelis, 1), dtype=np.float32)
    xirdp_ant = np.zeros((numberOfHelis*3, 1), dtype=np.float32)
    exil_ant = np.zeros((3, 1), dtype=np.float32)
    exir_ant = np.zeros((numberOfHelis*3, 1), dtype=np.float32)
    epsi_ant = np.zeros((numberOfHelis, 1), dtype=np.float32)
    he_ant = np.zeros((3, 1), dtype=np.float32)
    hp_ant = np.zeros((3, 1), dtype=np.float32)
    xcp_ant = np.zeros((3, 1), dtype=np.float32)
    pdp_ant = 0
    ffmean_ant = np.zeros((3, 1), dtype=np.float32)
    ffmeanf_ant = np.zeros((3, 1), dtype=np.float32)
    ffmeanpf_ant = np.zeros((3, 1), dtype=np.float32)

    # Teleoperation control gains
    Kmap = 5 * np.eye(3)
    Kmap[1,1] = Kmap[0,0] / 2
    Kmap[2,2] = Kmap[0,0] / 2
    Kmapr = Kmap[0,0] / 10

    # KM = 200*np.eye(3);
    KP = 10 * np.eye(3)
    Kpr = KP[0,0] / 20

    Alpha = 1.0 * np.eye(3)
    Alphar = Alpha[0,0] / 5

    KE = 4.0 * np.eye(3)

    endSimulation = False
    pHeli = Heli()
    ropePos = np.empty((3, numParticles), dtype=np.float32, order='F')
    ropeForce = np.empty(3, dtype=np.float32)
    artificialForce = np.empty(3, dtype=np.float32)

    # create an instance of an event
    eventTimer = threading.Event()

    def tick():
        eventTimer.set()

    timer1 = mmtimer(math.ceil(deltat*1000), tick)
    timer1.start(True)

    endSimulation = False
    k=0
    # Start the stopwatch / counter
    t_start = time.perf_counter()
    t_stop = 0.
    t_elapsed = 0.
    t_previous = 0.
    t_delta = 0.

    while ( not endSimulation ) and (t_elapsed < simulationTime):
        eventTimer.wait()
        eventTimer.clear()

        #-- run the control loop
        for i in range(numberOfHelis):
            getHeliState(byref(pHeli), i+1)
            yHeli[i,:,k] = rfn.structured_to_unstructured(np.ctypeslib.as_array(pHeli), copy=True)
            getArtificialForce(artificialForce, i+1)
            yFForce[i,:,k] = artificialForce
            getRopePosition(ropePos, i+1)
            yRope[i,:,:,k] = ropePos.T
            #getRopeForce(yForce[i,:,k], i+1)
            getRopeForce(ropeForce, i+1) 
            yForce[i,:,k] = ropeForce
            Kfiltro[i,0] = np.linalg.norm(yForce[i,:,k]) # Norm
        
        yFnorm[:,k] = Kfiltro.sum(axis=1) / filtro_n
        #print(f"yFnorm[:,k] = {yFnorm[:,k]}")
        Kfiltro = np.roll(Kfiltro,1,axis=0)

        #-- Read the virtual load velocity references from shared memory 
        getUserCommands(yXm[:,k])

        angle = cv[3,0]
        RotXm = np.array([[np.cos(angle), -np.sin(angle), 0.], [np.sin(angle), np.cos(angle), 0.], [0., 0., 1.]])
        InvRotXm = RotXm.T

        #-- Controller
        #-- Relative position variables
        for i in range(numberOfHelis):
            yXi[i,:,k] = [yHeli[i,0,k], yHeli[i,1,k], yHeli[i,2,k]]
            yXip[i,:,k] = [yHeli[i,3,k], yHeli[i,4,k], yHeli[i,5,k]]
            yPsi[i,k] = yHeli[i,14,k]
            yPsip[i,k] = yHeli[i,22,k]
            yQ[i*4:i*4+4,k] = np.concatenate((yXi[i,:,k], [yPsi[i,k]]))
            yQp[i*4:i*4+4,k] = np.concatenate((yXip[i,:,k], [yPsip[i,k]]))
            yTsimHeli[i,k] = yHeli[i,23,k]

        yXiprom[:,k] = 1 / numberOfHelis * np.sum(yXi[:,:,k], axis=0)

        yXir[:,:,k] = yXi[:,:,k] - np.tile(yXiprom[:,k].T, (numberOfHelis, 1))

        yXirv = np.reshape(yXir[:,:,k].T, (numberOfHelis*3,-1), order='F')
        yXil[:,k] = [yRope[0,-1,0,k], yRope[0,-1,1,k], yRope[0,-1,2,k]]  # Payload position

        yH[:,k] = yXiprom[:,k] - yXil[:,k]
        yXic = np.array([[cv[0,0]], [cv[1,0]], [cv[2,0]]])
        yXicNew[:,k] = np.squeeze(yXic)

        #-- Filter
        if(k==0):
            yXicNewp[:,k] = yXicNew[:,k] - yXic.T
            xcp_ant = yXicNewp[:,k]
        else:
            yXicNewp[:,k] = yXicNew[:,k]-yXicNew[:,k-1]
        yXicNewp[:,k] = filtro_xcp * yXicNewp[:,k].T + (1-filtro_xcp) * xcp_ant
        xcp_ant = yXicNewp[:,k]

        yExil[:,k] = [yXicNew[0,k] - yXil[0,k], yXicNew[1,k] - yXil[1,k], yXicNew[2,k] -  yXil[2,k]]

        #-- Filter
        if(k==0):
            exil_ant = yExil[:,k]
        yExil[:,k] = filtro_exil * yExil[:,k] + (1-filtro_exil) * exil_ant
        exil_ant = yExil[:,k]
        
        #-- Filter
        if(k==0):
            fn_ant = yFnorm[:,k]
        yFnorm[:,k] = filtro_fn * yFnorm[:,k] + (1-filtro_fn) * fn_ant.T
        fn_ant = yFnorm[:,k]

        yXird[2:numberOfHelis*3:3,k] = np.squeeze(yXirv[2:numberOfHelis*3:3] + k_f * (np.tile(np.sum(yFnorm[:,k]), (numberOfHelis, 1)) - (yFnorm[:,k].reshape(-1,1))/ro))

        yExir[:,k] = yXird[:,k] - yXirv.T

        #-- Filter
        if(k==0):
            exir_ant = yExir[:,k]
        yExir[:,k] = filtro_exir @ yExir[:,k] + (np.eye(numberOfHelis*3) - filtro_exir) @ exir_ant
        exir_ant = yExir[:,k]
        
        #-- State observer
        epsilon2 = 0.05
        K2 = np.eye(numberOfHelis*3)
        if(k==0):
            xird_ant = yXird[:,k]

        yXirde[:,k] = xird_ant
        yXirdp[:,k] = 1/epsilon2 * K2 @ (yXird[:,k] - yXirde[:,k])
        xird_ant = yXirde[:,k] + yXirdp[:,k] * deltat
        
        #-- Filter
        if(k==0):
            xirdp_ant = yXirdp[:,k]
        yXirdp[:,k] = filtro_xirdp * yXirdp[:,k] + (1-filtro_xirdp) * xirdp_ant.T
        xirdp_ant = yXirdp[:,k]
        yEpsi[:,k] = np.squeeze(np.tile(cv[3,0], (numberOfHelis,1)) - yPsi[:,k].reshape(-1,1))
        
        for i in range(numberOfHelis):
            while abs(yEpsi[i,k])>math.pi:
                if yEpsi[i,k]>math.pi:
                    yEpsi[i,k] = yEpsi[i,k]-2*math.pi
                elif  yEpsi[i,k]<-math.pi:
                    yEpsi[i,k] = yEpsi[i,k]+2*math.pi

        #-- Filter
        if(k==0):
            epsi_ant = yEpsi[:,k]
        yEpsi[:,k] = filtro_epsi * yEpsi[:,k]+ (1-filtro_epsi) * epsi_ant
        epsi_ant = yEpsi[:,k]

        yPdp[k]=cvp[3,0]

        #-- State observer
        epsilon4 = 0.05
        K4 = np.eye(3)
        if(k==0):
            he_ant = yH[:,k]
        yHe[:,k] = np.squeeze(he_ant)
        yHp[:,k] = np.squeeze(1/epsilon4 * K4 @ (yH[:,k] - yHe[:,k]).reshape(-1,1))
        he_ant = yHe[:,k] + yHp[:,k] * deltat
        
        #-- Filter
        if(k==0):
            hp_ant = yHp[:,k]
        yHp[:,k] = filtro_hp * yHp[:,k] + (1-filtro_hp) * hp_ant.T
        hp_ant = yHp[:,k]

        #-- Control laws -------------------------------------
        yVd[:,k] = cvp[0:3,0].T + ((Kffxyz @ Kffxyzt @ (1 - np.tanh(Kffxyzt @ yFFmeanf[:,k])**2)) @ yFFmeanpf[:,k].reshape(-1,1)).T
        yPath_vc[:,k] = yVd[:,k] + Kpath @ np.tanh(Kpatht @ yExil[:,k]) + yHp[:,k]
        yRel_vc[:,k] = yXirdp[:,k] + Krel @ np.tanh(Krelt @ yExir[:,k])
        yPsi_vc[:,k] = yPdp[k] + Kpsi @ np.tanh(Kpsit @ yEpsi[:,k])

        #-- Control laws of the full system
        Er = yRel_vc[0:-3,k].reshape(-1,1)
        El = yPath_vc[:,k].reshape(-1,1)
        Ep = yPsi_vc[:,k].reshape(-1,1)
        E = np.vstack((Er, El, Ep))

        #-- Unified control law
        yU[:,k] = np.squeeze(inv(J) @ E)

        for i in range(numberOfHelis):
            angle = yPsi[i,k]
            Juc = np.array([[np.cos(angle), np.sin(angle)], [-np.sin(angle), np.cos(angle)]])
            yUc[i,0:2,k] = np.squeeze(Juc @ np.vstack((yU[i*4,k], yU[i*4+1,k])))
            yUc[i,2:4,k] = np.squeeze(np.vstack((yU[i*4+2,k], yU[i*4+3,k])))


        Fe = (KE @ yExil[:,k]).reshape(-1,1)
        Fs = KP @ (Kmap @ yXm[0:3,k].reshape(-1,1) - InvRotXm @ cvp[0:3]) - Alpha @ InvRotXm @ cvpp[0:3] - Kffxyz @ InvRotXm @ np.mean(FFHelis, axis=0).reshape(-1,1) - InvRotXm @ Fe
        #Fm = -KM*(yXm(1:3,k) - Kmap\InvRotXm*cvp(1:3))
        Fsr= Kpr * (Kmapr * yXm[3,k] - cvp[3])
        
        cvpp[0:3] = RotXm @ Fs/payloadMass
        cvpp[3] = Fsr/payloadInertia
        
        cvp = cvp + cvpp*deltat
        cv = cv + cvp*deltat
        
        if(cv[2] < 0):
            cv[2]=0
            cvp[2]=0
        
        yXYZd[:,k] = np.squeeze(cv[0:3])
        yXYZdp[:,k]= np.squeeze(cvp[0:3])
        
        yPsid[0,k] = cv[3,0]
        yPsidp[0,k] = cvp[3,0]

        setVirtualLoad(cv)

        for i in range(numberOfHelis):
            #-- Rotated velocity references for the helis low-level controller (vxh, vyh, vzh, wzh)
            setHeliCommands(yUc[i,0,k], yUc[i,1,k], yUc[i,2,k], yUc[i,3,k], i+1)
        
        t_stop = time.perf_counter()
        t_delta = t_stop - t_previous
        t_previous = t_stop
        t_elapsed = t_stop - t_start
        yTsim[k] = t_elapsed
        k += 1
        #print(k)
        #end of while loop

    for i in range(numberOfHelis):
        #-- Set heleis velocity references to 0
        setHeliCommands(0., 0., 0., 0., i+1)
    timer1.stop()
    stopSimulation(True)
    yTsim = yTsim - yTsim[0] # shift to start time in 0 seconds
    print(f"Total elapsed time: {t_elapsed} seconds")
    
    #Terminate the C++ program
    time.sleep(.1)
    proc.terminate()
        
    plot = True
    
    if plot:
        #Time steps figure
        ydata = np.concatenate(([0.], np.diff(yTsim[0:k])))
        plt.scatter(yTsim[0:k], ydata)
        #add horizontal line at mean value of y
        plt.axhline(y=np.mean(ydata), color='red', linestyle='--', linewidth=3, label='Avg')
        plt.xlabel("Time [sec]")
        plt.ylabel("Simulation time steps [sec]")
        plt.legend()
        plt.show()

        #Cables' tension figure
        for i in range(numberOfHelis):
            plt.plot(yTsim[0:k], yFnorm[i,0:k], label=f"Heli {i+1}")
        plt.xlabel("Time [sec]")
        plt.ylabel("Cable tension [N]")
        plt.legend()
        plt.show()

        #Payload tracking error figure
        plt.plot(yTsim[0:k], np.squeeze(np.linalg.norm(yExil[:,0:k], axis=0))) # Norm, color='black', linewidth=1.5)
        plt.xlabel("Time [sec]")
        plt.ylabel(r'$\|\tilde{\xi}_\ell\|$ $[m]$')
        plt.show()

if __name__ == "__main__":
   #-- run the application
   run()